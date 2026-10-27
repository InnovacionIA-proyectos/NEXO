namespace Nexo.Domain.Asistente.Entidades;

/// <summary>
/// Un cambio que el asistente PROPONE y que solo se aplica cuando la persona lo confirma. El modelo nunca escribe en NEXO: deja aquí lo
/// que quiere hacer, con parámetros fijos que ya no cambian, y la persona decide. Es también la auditoría de cada escritura: quién,
/// cuándo, qué y cómo terminó.
/// </summary>
public class clsAccionAsistente
{
    public const string TipoAgregarFlete = "agregar_flete";
    public const string TipoEditarFlete = "editar_flete";
    public const string TipoAgregarConexos = "agregar_conexos";
    public const string TipoRechazarLinea = "rechazar_linea";
    public const string TipoCambiarVehiculo = "cambiar_vehiculo";

    /// <summary>Propuesta, esperando que la persona confirme o cancele. Nada se ha tocado.</summary>
    public const string EstadoPendiente = "pendiente";

    /// <summary>La persona confirmó y se está aplicando; ya no se puede confirmar otra vez.</summary>
    public const string EstadoEjecutando = "ejecutando";

    public const string EstadoEjecutada = "ejecutada";

    /// <summary>Se confirmó pero una regla del dominio la rechazó (<see cref="ResultadoCodigo"/> dice cuál). No se aplicó nada.</summary>
    public const string EstadoFallida = "fallida";

    public const string EstadoCancelada = "cancelada";

    /// <summary>Pasó el tiempo de vigencia sin confirmarse.</summary>
    public const string EstadoVencida = "vencida";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversacionId { get; set; }

    /// <summary>La respuesta del asistente en la que se propuso.</summary>
    public Guid MensajeId { get; set; }

    /// <summary>Quién puede confirmarla: la persona de la conversación y nadie más.</summary>
    public Guid UsuarioId { get; set; }

    public string Tipo { get; set; } = null!;

    /// <summary>Los parámetros del cambio, como JSON, tal como los validó el servidor. Lo que se confirma es exactamente esto.</summary>
    public string Parametros { get; set; } = null!;

    /// <summary>Lo que se le muestra a la persona para que decida.</summary>
    public string Resumen { get; set; } = null!;

    public string Estado { get; set; } = EstadoPendiente;
    public DateTimeOffset VenceAt { get; set; }
    public DateTimeOffset? ResueltaAt { get; set; }

    /// <summary><c>ok</c>, o el código del rechazo del dominio (<c>FLETE_SOLO_SICETAC</c>, <c>COTIZACION_NO_EDITABLE</c>…).</summary>
    public string? ResultadoCodigo { get; set; }

    public string? ResultadoMensaje { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
