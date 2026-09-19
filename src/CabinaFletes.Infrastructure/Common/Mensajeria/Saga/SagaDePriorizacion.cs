using CabinaFletes.Mensajes.Priorizacion;
using MassTransit;

namespace CabinaFletes.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// El reloj del recálculo de scores, viviendo en el broker en vez de en el proceso.
///
/// SAGA PURA, como la del auto-costeo: aquí no se calcula ningún score ni se toca la
/// base. Solo decide CUÁNDO hay que recalcular y publica el pedido. El trabajo lo hace
/// un consumidor, con sus reintentos y su cola de error.
///
/// POR QUÉ UNA SAGA Y NO UN MENSAJE QUE SE REPROGRAMA SOLO. RabbitMQ con el plugin de
/// retardo no sabe de mensajes recurrentes — para eso MassTransit necesita Quartz o
/// Hangfire, y no hacía falta traer eso. La alternativa era que el consumidor programara
/// el siguiente al terminar, pero entonces cada arranque del API y cada réplica arranca
/// su propia cadena, la frecuencia se duplica sola y nadie se entera.
///
/// Con la saga eso no puede pasar: el CorrelationId es constante, así que la fila de la
/// saga ES el candado. Un segundo arranque no crea una cadena nueva, le vuelve a armar
/// el reloj a la que ya existe.
///
/// Y ese rearmado es a propósito, no un efecto colateral: hace que arrancar el API
/// REPARE la cadena si el temporizador se perdió — por ejemplo si alguien borró el
/// volumen del broker. Sin él, la cadena quedaría muerta en silencio y el síntoma
/// aparecería semanas después, en un reporte con scores viejos.
/// </summary>
/// <summary>
/// Cada cuánto corre el recálculo.
///
/// Es un tipo propio y no un <c>TimeSpan</c> suelto porque MassTransit construye la
/// máquina de estados con el contenedor, y un TimeSpan no se puede inyectar: no hay forma
/// de distinguir "el intervalo del recálculo" de cualquier otro TimeSpan registrado.
///
/// Se le pasa resuelto en vez de que la saga lea la configuración por su cuenta, para que
/// una prueba pueda instanciarla con 300 ms sin tener que armarle un IConfiguration.
/// </summary>
public sealed record IntervaloDeRecalculo(TimeSpan Valor);

public sealed class SagaDePriorizacion : MassTransitStateMachine<EstadoPriorizacion>
{
    /// <summary>Clave de configuración heredada del servicio que esto reemplaza.</summary>
    public const string ClaveIntervalo = "Score:IntervaloRecalculoMinutos";

    public const int MinutosPorDefecto = 15;

    public State Programado { get; private set; } = null!;

    public Event<ArrancarPriorizacion> Arrancar { get; private set; } = null!;
    public Event<ParametrosDePriorizacionCambiaron> ParametrosCambiaron { get; private set; } = null!;

    public Schedule<EstadoPriorizacion, TicDePriorizacion> Tic { get; private set; } = null!;

    public SagaDePriorizacion(IntervaloDeRecalculo intervalo)
    {
        InstanceState(x => x.CurrentState);

        // Todo se correlaciona por la constante. No hay identificador por arranque, y de
        // eso depende que no puedan existir dos cadenas.
        Event(() => Arrancar, x => x.CorrelateById(m => m.Message.CadenaId));
        Event(() => ParametrosCambiaron, x => x.CorrelateById(m => m.Message.CadenaId));

        Schedule(() => Tic, x => x.TokenTic, x =>
        {
            x.Delay = intervalo.Valor;
            x.Received = e => e.CorrelateById(m => m.Message.CadenaId);
        });

        Initially(
            When(Arrancar)
                .Then(c =>
                {
                    c.Saga.ArmadaEn = DateTimeOffset.UtcNow;
                    c.Saga.RearmadaEn = c.Saga.ArmadaEn;
                })
                .Schedule(Tic, c => new TicDePriorizacion(c.Saga.CorrelationId))
                .TransitionTo(Programado),

            // Recalibrar antes de que la cadena exista es raro pero posible: el mensaje
            // podría adelantarse al arranque. Sin esta rama MassTransit lo daría por no
            // aceptado, agotaría reintentos y lo mandaría a la cola de error — o sea, una
            // alerta al equipo por algo que no está roto.
            When(ParametrosCambiaron)
                .Then(c =>
                {
                    c.Saga.ArmadaEn = DateTimeOffset.UtcNow;
                    c.Saga.RearmadaEn = c.Saga.ArmadaEn;
                })
                .Then(RegistrarRecalculoPorParametros)
                .Publish(c => new RecalcularScoresSolicitado(
                    c.Saga.CorrelationId, CadenaDePriorizacion.PorCambioDeParametros))
                .Schedule(Tic, c => new TicDePriorizacion(c.Saga.CorrelationId))
                .TransitionTo(Programado));

        During(Programado,
            // Rearmar. El temporizador anterior queda descartado por su token, así que
            // esto no puede duplicar la frecuencia.
            When(Arrancar)
                .Then(c => c.Saga.RearmadaEn = DateTimeOffset.UtcNow)
                .Schedule(Tic, c => new TicDePriorizacion(c.Saga.CorrelationId)),

            When(Tic.Received)
                .Then(c =>
                {
                    c.Saga.UltimoRecalculoEn = DateTimeOffset.UtcNow;
                    c.Saga.RecalculosPorReloj++;
                })
                .Publish(c => new RecalcularScoresSolicitado(
                    c.Saga.CorrelationId, CadenaDePriorizacion.PorElReloj))
                // Se reprograma SIEMPRE, incluso si el recálculo de esta vuelta falla.
                // El fallo lo resuelven los reintentos del consumidor; si la cadena se
                // detuviera al primer problema, un fallo transitorio dejaría los scores
                // congelados hasta el siguiente reinicio del API.
                .Schedule(Tic, c => new TicDePriorizacion(c.Saga.CorrelationId)),

            When(ParametrosCambiaron)
                .Then(RegistrarRecalculoPorParametros)
                .Publish(c => new RecalcularScoresSolicitado(
                    c.Saga.CorrelationId, CadenaDePriorizacion.PorCambioDeParametros)));
    }

    /// <summary>
    /// Intervalo configurable con la misma clave que usaba el servicio anterior, para que
    /// quien ya la tenga puesta no tenga que cambiar nada. Acotado entre un minuto y un
    /// día: por debajo se convierte en carga constante sobre la base, por encima deja de
    /// ser un refresco.
    /// </summary>
    public static TimeSpan IntervaloDe(int minutos) =>
        TimeSpan.FromMinutes(Math.Clamp(minutos, 1, 1440));

    private static void RegistrarRecalculoPorParametros(BehaviorContext<EstadoPriorizacion, ParametrosDePriorizacionCambiaron> c)
    {
        c.Saga.UltimoRecalculoEn = DateTimeOffset.UtcNow;
        c.Saga.RecalculosPorParametros++;
    }
}
