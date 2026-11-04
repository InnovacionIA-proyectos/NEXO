using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria.Saga;

/// <summary>
/// Estado persistido del trabajo de auto-costeo, consultable por el identificador devuelto en el 202.
/// PendientesTecnicas (una fuente falló y se reintenta) y RequierenManual (sin tarifa válida, espera al Administrador) van separados porque el usuario actúa distinto en cada caso.
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

    /// <summary>Total informado por el reparto; nulo hasta entonces, cuando la saga aún no sabe cuándo estaría completa.</summary>
    public int? TotalLineas { get; set; }

    public int Resueltas { get; set; }
    public int PendientesTecnicas { get; set; }
    public int RequierenManual { get; set; }

    /// <summary>Se incrementa en cada cambio; el cliente descarta avisos rezagados, que pueden llegar al navegador en otro orden.</summary>
    public int Secuencia { get; set; }

    /// <summary>Token del timeout, para poder cancelarlo si el trabajo termina antes.</summary>
    public Guid? TokenExpiracion { get; set; }

    /// <summary>Cuántas líneas ya tienen desenlace, del tipo que sea.</summary>
    public int ConDesenlace => Resueltas + PendientesTecnicas + RequierenManual;

    /// <summary>Todas las líneas tienen desenlace; no implica éxito (todas pueden requerir tarifa manual).</summary>
    public bool TodasConDesenlace => TotalLineas is { } total && ConDesenlace >= total;

    /// <summary>Única condición para cerrar en Completado: sin reintentos abiertos ni líneas esperando a una persona.</summary>
    public bool TodoResueltoPorElServicio => PendientesTecnicas == 0 && RequierenManual == 0;
}
