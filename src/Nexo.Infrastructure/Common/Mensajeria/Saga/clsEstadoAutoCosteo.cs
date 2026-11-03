using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Estado persistido del trabajo de auto-costeo de un pliego.
///
/// Es el registro que hace que el trabajo sea consultable: el cliente recibió un
/// identificador con el 202 y pregunta por él. Si esto no se persistiera, un reinicio
/// del proceso dejaría al cliente preguntando por un trabajo que ya nadie recuerda.
///
/// Los contadores están separados a propósito y no son un solo "pendientes":
///
///   PendientesTecnicas  — una fuente falló, se está reintentando, se resuelve solo.
///   RequierenManual     — no hay tarifa válida; espera al Administrador de Tarifas.
///
/// La pantalla dice cosas distintas y el usuario hace cosas distintas con cada una.
/// Con un contador único habría que mentirle en la mitad de los casos.
/// </summary>
public class clsEstadoAutoCosteo : SagaStateMachineInstance, ISagaVersion
{
    /// <summary>El identificador del trabajo que se devolvió en el 202.</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Concurrencia optimista: dos resultados de línea pueden llegar a la vez.</summary>
    public int Version { get; set; }

    public string CurrentState { get; set; } = null!;

    public Guid SolicitudId { get; set; }
    public Guid SolicitadoPor { get; set; }
    public DateTimeOffset SolicitadoEn { get; set; }
    public DateTimeOffset? TerminadoEn { get; set; }

    /// <summary>
    /// Total autoritativo, el que informó el reparto. Nulo mientras no se haya
    /// repartido: hasta entonces la saga no sabe cuándo estaría completa.
    /// </summary>
    public int? TotalLineas { get; set; }

    public int Resueltas { get; set; }
    public int PendientesTecnicas { get; set; }
    public int RequierenManual { get; set; }

    /// <summary>
    /// Se incrementa en CADA cambio. Viaja en el aviso para que el cliente descarte los
    /// rezagados.
    ///
    /// Hace falta porque dos avisos concurrentes pueden llegar al navegador en orden
    /// distinto al que se generaron, y sin un número que solo crece la barra de progreso
    /// retrocedería. Que el mensaje lleve solo el identificador evita datos viejos en el
    /// mensaje, pero no reordena las entregas.
    /// </summary>
    public int Secuencia { get; set; }

    /// <summary>Token del timeout, para poder cancelarlo si el trabajo termina antes.</summary>
    public Guid? TokenExpiracion { get; set; }

    /// <summary>Cuántas líneas ya tienen desenlace, del tipo que sea.</summary>
    public int ConDesenlace => Resueltas + PendientesTecnicas + RequierenManual;

    /// <summary>
    /// Todas las líneas tienen desenlace. No implica éxito: un pliego donde todas
    /// requieren tarifa manual también está "completo" en este sentido.
    /// </summary>
    public bool TodasConDesenlace => TotalLineas is { } total && ConDesenlace >= total;

    /// <summary>
    /// El servicio resolvió todo por su cuenta, sin reintentos abiertos ni líneas
    /// esperando a una persona. Es la única condición para cerrar en Completado.
    /// </summary>
    public bool TodoResueltoPorElServicio => PendientesTecnicas == 0 && RequierenManual == 0;
}
