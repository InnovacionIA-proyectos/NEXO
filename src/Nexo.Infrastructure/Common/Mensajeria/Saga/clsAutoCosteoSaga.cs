using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Costeo;
using Nexo.Mensajes.Solicitudes;
using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Máquina de estados del auto-costeo de un pliego. Saga pura: solo orquesta (pide repartir, cuenta desenlaces y decide cuándo y cómo termina); no cuesta ni consulta la base.
/// El timeout mide la parte automática: una línea que espera al Administrador de Tarifas no tiene reloj, y si vence con líneas sin desenlace el trabajo cierra en Parcial.
/// </summary>
public sealed class clsAutoCosteoSaga : MassTransitStateMachine<clsEstadoAutoCosteo>
{
    public State Procesando { get; private set; } = null!;
    public State Completado { get; private set; } = null!;
    public State Parcial { get; private set; } = null!;

    public Event<clsAutoCosteoSolicitado> Solicitado { get; private set; } = null!;
    public Event<clsPliegoRepartido> Repartido { get; private set; } = null!;
    public Event<clsLineaCosteada> LineaResuelta { get; private set; } = null!;
    public Event<clsLineaFalloTecnico> LineaConFalloTecnico { get; private set; } = null!;
    public Event<clsLineaRequiereTarifaManual> LineaEsperandoPersona { get; private set; } = null!;

    public Schedule<clsEstadoAutoCosteo, clsExpiracionAutoCosteo> Expiracion { get; private set; } = null!;

    public clsAutoCosteoSaga()
    {
        InstanceState(x => x.CurrentState);

        // Todo se correlaciona por el identificador del trabajo (el mismo que el cliente recibió con el 202).
        Event(() => Solicitado, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => Repartido, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaResuelta, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaConFalloTecnico, x => x.CorrelateById(m => m.Message.TrabajoId));
        Event(() => LineaEsperandoPersona, x => x.CorrelateById(m => m.Message.TrabajoId));

        Schedule(() => Expiracion, x => x.TokenExpiracion, x =>
        {
            // Ventana de la parte automática: generosa porque un pliego de 300 líneas consulta cuatro fuentes externas.
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
                .Schedule(Expiracion, c => new clsExpiracionAutoCosteo(c.Saga.CorrelationId))
                .Publish(c => new clsRepartirLineasDeCosteo(c.Saga.CorrelationId, c.Saga.SolicitudId))
                .Then(c => c.Saga.Secuencia++)
                .Publish(c => new clsTrabajoActualizado(c.Saga.CorrelationId))
                .TransitionTo(Procesando));

        During(Procesando,
            // El reparto puede llegar después de algunos resultados: hasta conocer el total no se sabe si ya terminó, por eso el cierre se evalúa aquí también.
            Avisar(CerrarSiTermino(When(Repartido).Then(c => c.Saga.TotalLineas = c.Message.TotalLineas))),
            Avisar(CerrarSiTermino(When(LineaResuelta).Then(c => c.Saga.Resueltas++))),
            Avisar(CerrarSiTermino(When(LineaConFalloTecnico).Then(c => c.Saga.PendientesTecnicas++))),
            Avisar(CerrarSiTermino(When(LineaEsperandoPersona).Then(c => c.Saga.RequierenManual++))),

            When(Expiracion.Received)
                .Then(c =>
                {
                    c.Saga.TerminadoEn = DateTimeOffset.UtcNow;
                    c.Saga.Secuencia++;
                })
                .TransitionTo(Parcial)
                .Publish(c => new clsTrabajoActualizado(c.Saga.CorrelationId))
                // Vencer el plazo también cierra el trabajo y saca la fila del filtro de pendientes.
                .Publish(c => new clsBandejaCambio(clsMotivoDeCambio.TrabajoTerminado, DateTimeOffset.UtcNow)));

        // Un mensaje que llega tras el cierre (reintento tardío, timeout que vence mientras se cierra) se ignora en vez de reventar la saga.
        During(Completado, Parcial,
            Ignore(Repartido),
            Ignore(LineaResuelta),
            Ignore(LineaConFalloTecnico),
            Ignore(LineaEsperandoPersona),
            Ignore(Expiracion.Received));

        // No se usa SetCompletedWhenFinalized(): borraría la instancia y el estado es lo que el cliente consulta después.
    }

    /// <summary>
    /// Avisa en cada cambio (no solo en hitos) con un mensaje que lleva solo el identificador; si un pliego supera ~2.000 líneas habría que agrupar los avisos.
    /// </summary>
    private static EventActivityBinder<clsEstadoAutoCosteo, T> Avisar<T>(
        EventActivityBinder<clsEstadoAutoCosteo, T> binder) where T : class =>
        binder
            .Then(c => c.Saga.Secuencia++)
            .Publish(c => new clsTrabajoActualizado(c.Saga.CorrelationId));

    /// <summary>
    /// Cuando todas las líneas tienen desenlace apaga el timeout y cierra: Completado solo si el servicio resolvió todo; si no, Parcial. Fallido se reserva para fallos del servicio mismo.
    /// </summary>
    private EventActivityBinder<clsEstadoAutoCosteo, T> CerrarSiTermino<T>(
        EventActivityBinder<clsEstadoAutoCosteo, T> binder) where T : class =>
        binder.If(c => c.Saga.TodasConDesenlace, cerrar => cerrar
            .Then(c => c.Saga.TerminadoEn = DateTimeOffset.UtcNow)
            .Unschedule(Expiracion)
            // La bandeja cambia aquí y no en cada línea: es cuando el pliego deja de tener líneas sin flete; solo necesita saber que reconsulte.
            .Publish(c => new clsBandejaCambio(clsMotivoDeCambio.TrabajoTerminado, DateTimeOffset.UtcNow))
            .IfElse(c => c.Saga.TodoResueltoPorElServicio,
                completo => completo.TransitionTo(Completado),
                parcial => parcial.TransitionTo(Parcial)));
}

/// <summary>Vence la ventana de la parte automática; lo entrega el broker (no un temporizador en memoria), así sobrevive a un reinicio.</summary>
public sealed record clsExpiracionAutoCosteo(Guid TrabajoId);
