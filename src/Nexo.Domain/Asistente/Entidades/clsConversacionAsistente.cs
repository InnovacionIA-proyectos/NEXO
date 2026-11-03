namespace Nexo.Domain.Asistente.Entidades;

/// <summary>
/// Una conversación de una persona con el asistente de cotización. Es de quien la inicia: nadie más escribe en ella.
/// <see cref="Turnos"/> cuenta los mensajes del usuario y es lo que se limita por conversación.
/// </summary>
public class clsConversacionAsistente
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UsuarioId { get; set; }

    /// <summary>Las primeras palabras del primer mensaje, para reconocerla en un listado.</summary>
    public string Titulo { get; set; } = null!;

    public int Turnos { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Un mensaje de la conversación. <see cref="Orden"/> es único dentro de ella.</summary>
public class clsMensajeAsistente
{
    public const string RolUsuario = "user";
    public const string RolAsistente = "assistant";

    /// <summary>La respuesta se está generando.</summary>
    public const string EstadoEnCurso = "en_curso";
    public const string EstadoCompleto = "completo";

    /// <summary>La respuesta se cortó (el usuario cerró la conexión, se acabó el espacio de respuesta o falló el proveedor).</summary>
    public const string EstadoIncompleto = "incompleto";

    /// <summary>La respuesta traía cifras que no vienen de ninguna consulta y se reemplazó por un aviso; el texto original no se guarda.</summary>
    public const string EstadoDescartado = "descartado";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversacionId { get; set; }
    public int Orden { get; set; }
    public string Rol { get; set; } = null!;
    public string Contenido { get; set; } = string.Empty;
    public string Estado { get; set; } = EstadoCompleto;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Auditoría de una herramienta que el asistente usó: cuál, con qué entrada, qué devolvió y cómo terminó. Guarda lo que el
/// asistente vio, que es la prueba de dónde salió cada valor de una respuesta.
/// </summary>
public class clsUsoHerramientaAsistente
{
    public const string ResultadoOk = "ok";

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversacionId { get; set; }

    /// <summary>La respuesta del asistente en cuyo transcurso se usó.</summary>
    public Guid MensajeId { get; set; }

    public string Herramienta { get; set; } = null!;

    /// <summary>La entrada que mandó el modelo, como JSON.</summary>
    public string Entrada { get; set; } = null!;

    /// <summary>Lo que devolvió la herramienta, como JSON; null si falló antes de devolver algo.</summary>
    public string? Salida { get; set; }

    public bool? Encontrado { get; set; }
    public string? Fuente { get; set; }

    /// <summary><c>ok</c> o el código del error (<c>HERRAMIENTA_NO_PERMITIDA</c>, <c>SOLICITUD_AJENA</c>, <c>HERRAMIENTA_ERROR</c>…).</summary>
    public string Resultado { get; set; } = null!;

    public int LatenciaMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
