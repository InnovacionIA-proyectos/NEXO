using CabinaFletes.Mensajes.Costeo;
using CabinaFletes.Mensajes.Solicitudes;
using MassTransit;

namespace CabinaFletes.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Máquina de estados del auto-costeo de un pliego.
///
/// SAGA PURA: aquí no hay una sola regla de negocio. No cuesta, no consulta la base,
/// no decide tarifas, no sabe qué es un piso SICE-TAC. Solo orquesta: pide repartir,
/// cuenta desenlaces y decide cuándo el trabajo terminó y en qué estado. Todo lo demás
/// vive fuera, en los consumidores.
///
/// Por eso el reparto es un mensaje y no una consulta: si la saga leyera las líneas de
/// la base, dejaría de ser pura y empezaría a acumular lógica.
///
/// EL TIMEOUT MIDE MÁQUINA, NO PERSONAS. Se programa para lo que el servicio puede
/// hacer solo, cuya duración es predecible. Una línea que espera al Administrador de
/// Tarifas NO tiene reloj: esa persona puede tener otros pliegos delante, y eso no es
/// algo que un timeout deba resolver. Si vence con líneas sin desenlace, el trabajo
/// cierra en Parcial — que es un desenlace previsto, no un fallo.
/// </summary>
public sealed class AutoCosteoSaga : MassTransitStateMachine<EstadoAutoCosteo>
{
    public State Procesando { get; private set; } = null!;
    public State Completado { get; private set; } = null!;
    public State Parcial { get; private set; } = null!;

    public Event<AutoCosteoSolicitado> Solicitado { get; private set; } = null!;
    public Event<PliegoRepartido> Repartido { get; private set; } = null!;
    public Event<LineaCosteada> LineaResuelta { get; private set; } = null!;
    public Event<LineaFalloTecnico> LineaConFalloTecnico { get; private set; } = null!;
    public Event<LineaRequiereTarifaManual> LineaEsperandoPersona { get; private set; } = null!;

    public Schedule<EstadoAutoCosteo, ExpiracionAutoCosteo> Expiracion { get; private set; } = null!;

    public AutoCosteoSaga()
    {
        InstanceState(x => x.CurrentState);

        // Todo se correlaciona por el identificador del trabajo, que es el mismo que el
        // cliente recibió con el 202.
        Event(() => Solicitado, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => Repartido, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaResuelta, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaConFalloTecnico, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaEsperandoPersona, x => x.CorrelateById(m => m.Message.TrabajoId));

        Schedule(() => Expiracion, x => x.TokenExpiracion, x =>
        {
            // Ventana de la parte automática. Generosa porque un pliego de 300 líneas
            // consulta cuatro fuentes externas, una de ellas sobre SQL Server 2008 R2.
            x.Delay = TimeSpan.FromMinutes(30);
            x.Received = e => e.CorrelateById(m => m.Message.TrabajoId);
        });

        Initially(
            When(Solicitado)
                .Then(c =>
                {
                    c.Saga.SolicitudId = c.Message.SolicitudId;
                    c.Saga.SolicitadoPor = c.Message.SolicitadoPor;
                    c.Saga.SolicitadoEn = c.Message.SolicitadoEn;
                })
                .Schedule(Expiracion, c => new ExpiracionAutoCosteo(c.Saga.CorrelationId))
                .Publish(c => new RepartirLineasDeCosteo(c.Saga.CorrelationId, c.Saga.SolicitudId))
                .Publish(c => new TrabajoActualizado(c.Saga.CorrelationId))
                .TransitionTo(Procesando));

        During(Procesando,
            // El reparto puede llegar DESPUÉS de algunos resultados: hasta que no se
            // sabe el total, la saga no puede saber si ya terminó. De ahí que el cierre
            // se evalúe también aquí.
            Avisar(CerrarSiTermino(When(Repartido).Then(c => c.Saga.TotalLineas = c.Message.TotalLineas))),
            Avisar(CerrarSiTermino(When(LineaResuelta).Then(c => c.Saga.Resueltas++))),
            Avisar(CerrarSiTermino(When(LineaConFalloTecnico).Then(c => c.Saga.PendientesTecnicas++))),
            Avisar(CerrarSiTermino(When(LineaEsperandoPersona).Then(c => c.Saga.RequierenManual++))),

            When(Expiracion.Received)
                .Then(c => c.Saga.TerminadoEn = DateTimeOffset.UtcNow)
                .TransitionTo(Parcial)
                .Publish(c => new TrabajoActualizado(c.Saga.CorrelationId)));

        // Un trabajo ya cerrado no se reabre por un mensaje que llegó tarde: se ignora
        // en vez de reventar la saga. Pasa de verdad — un reintento que se resolvió
        // después del cierre, o el timeout que venció justo mientras se cerraba.
        During(Completado, Parcial,
            Ignore(Repartido),
            Ignore(LineaResuelta),
            Ignore(LineaConFalloTecnico),
            Ignore(LineaEsperandoPersona),
            Ignore(Expiracion.Received));

        // NO se llama a SetCompletedWhenFinalized(): eso BORRARÍA la instancia al
        // terminar, y el estado es justamente lo que el cliente consulta después. Un
        // trabajo terminado tiene que seguir siendo consultable.
    }

    /// <summary>
    /// Anuncia que el trabajo cambió, para que quien lo esté mirando lo vea.
    ///
    /// Se avisa en CADA cambio y no solo en hitos: una barra que salta de cero a cien no
    /// informa nada, y quien espera un pliego largo necesita ver que algo se mueve.
    ///
    /// El costo está medido y es asumible a la escala del PRD: el mensaje lleva solo el
    /// identificador, así que un pliego de 300 líneas son 300 mensajes diminutos. Si algún
    /// día un pliego pasa de ~2.000 líneas, o el Outbox se queda con más de ~5.000 filas
    /// vivas, toca agrupar los avisos. Está en una sola actividad justamente para que ese
    /// cambio sea de una línea.
    /// </summary>
    private static EventActivityBinder<EstadoAutoCosteo, T> Avisar<T>(
        EventActivityBinder<EstadoAutoCosteo, T> binder) where T : class =>
        binder.Publish(c => new TrabajoActualizado(c.Saga.CorrelationId));

    /// <summary>
    /// Cuando todas las líneas tienen desenlace, cierra el trabajo: apaga el timeout y
    /// transiciona al estado honesto.
    ///
    /// Completado solo si el servicio resolvió TODO por su cuenta. Si algo quedó
    /// pendiente —por reintento o por esperar a una persona— el desenlace es Parcial.
    /// Fallido queda reservado para cuando se rompe el servicio mismo, no para cuando
    /// simplemente no había tarifa.
    /// </summary>
    private EventActivityBinder<EstadoAutoCosteo, T> CerrarSiTermino<T>(
        EventActivityBinder<EstadoAutoCosteo, T> binder) where T : class =>
        binder.If(c => c.Saga.TodasConDesenlace, cerrar => cerrar
            .Then(c => c.Saga.TerminadoEn = DateTimeOffset.UtcNow)
            .Unschedule(Expiracion)
            .IfElse(c => c.Saga.TodoResueltoPorElServicio,
                completo => completo.TransitionTo(Completado),
                parcial => parcial.TransitionTo(Parcial)));
}

/// <summary>
/// Vence la ventana de la parte automática del trabajo. Es un mensaje porque el
/// planificador de MassTransit lo entrega por el broker, no por un temporizador en
/// memoria: así sobrevive a un reinicio del proceso.
/// </summary>
public sealed record ExpiracionAutoCosteo(Guid TrabajoId);
