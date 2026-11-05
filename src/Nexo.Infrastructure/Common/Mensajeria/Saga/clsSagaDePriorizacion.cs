using Nexo.Mensajes.Priorizacion;
using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Reloj del recálculo de scores en el broker. Saga pura: solo decide cuándo recalcular y publica el pedido.
/// El CorrelationId es constante, así que la fila es el candado: un segundo arranque rearma el reloj de la cadena existente
/// (y la repara si el temporizador se perdió) en vez de crear otra.
/// </summary>
/// <summary>
/// Cada cuánto corre el recálculo. Tipo propio porque un TimeSpan suelto no se distingue en el contenedor;
/// se pasa resuelto para instanciar la saga en pruebas sin IConfiguration.
/// </summary>
public sealed record clsIntervaloDeRecalculo(TimeSpan Valor);

public sealed class clsSagaDePriorizacion : MassTransitStateMachine<clsEstadoPriorizacion>
{
    public const string ClaveIntervalo = "Score:IntervaloRecalculoMinutos";

    public const int MinutosPorDefecto = 15;

    public State Programado { get; private set; } = null!;

    public Event<clsArrancarPriorizacion> Arrancar { get; private set; } = null!;
    public Event<clsParametrosDePriorizacionCambiaron> ParametrosCambiaron { get; private set; } = null!;

    public Schedule<clsEstadoPriorizacion, clsTicDePriorizacion> Tic { get; private set; } = null!;

    public clsSagaDePriorizacion(clsIntervaloDeRecalculo intervalo)
    {
        InstanceState(x => x.CurrentState);

        // Todo se correlaciona por la constante: de eso depende que no puedan existir dos cadenas.
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
                .Schedule(Tic, c => new clsTicDePriorizacion(c.Saga.CorrelationId))
                .TransitionTo(Programado),

            // Recalibrar antes del arranque es raro pero posible; sin esta rama el mensaje se rechazaría, agotaría reintentos y alertaría por algo que no está roto.
            When(ParametrosCambiaron)
                .Then(c =>
                {
                    c.Saga.ArmadaEn = DateTimeOffset.UtcNow;
                    c.Saga.RearmadaEn = c.Saga.ArmadaEn;
                })
                .Then(RegistrarRecalculoPorParametros)
                .Publish(c => new clsRecalcularScoresSolicitado(
                    c.Saga.CorrelationId, clsCadenaDePriorizacion.PorCambioDeParametros))
                .Schedule(Tic, c => new clsTicDePriorizacion(c.Saga.CorrelationId))
                .TransitionTo(Programado));

        During(Programado,
            // Rearmar: el temporizador anterior queda descartado por su token, así que no duplica la frecuencia.
            When(Arrancar)
                .Then(c => c.Saga.RearmadaEn = DateTimeOffset.UtcNow)
                .Schedule(Tic, c => new clsTicDePriorizacion(c.Saga.CorrelationId)),

            When(Tic.Received)
                .Then(c =>
                {
                    c.Saga.UltimoRecalculoEn = DateTimeOffset.UtcNow;
                    c.Saga.RecalculosPorReloj++;
                })
                .Publish(c => new clsRecalcularScoresSolicitado(
                    c.Saga.CorrelationId, clsCadenaDePriorizacion.PorElReloj))
                // Se reprograma siempre, incluso si el recálculo falla: de lo contrario un fallo transitorio dejaría los scores congelados hasta reiniciar el API.
                .Schedule(Tic, c => new clsTicDePriorizacion(c.Saga.CorrelationId)),

            When(ParametrosCambiaron)
                .Then(RegistrarRecalculoPorParametros)
                .Publish(c => new clsRecalcularScoresSolicitado(
                    c.Saga.CorrelationId, clsCadenaDePriorizacion.PorCambioDeParametros)));
    }

    /// <summary>Acotado entre un minuto y un día: por debajo es carga constante sobre la base, por encima deja de ser un refresco.</summary>
    public static TimeSpan IntervaloDe(int minutos) =>
        TimeSpan.FromMinutes(Math.Clamp(minutos, 1, 1440));

    private static void RegistrarRecalculoPorParametros(BehaviorContext<clsEstadoPriorizacion, clsParametrosDePriorizacionCambiaron> c)
    {
        c.Saga.UltimoRecalculoEn = DateTimeOffset.UtcNow;
        c.Saga.RecalculosPorParametros++;
    }
}
