namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>
/// Límites de una conversación con el asistente (sección <c>Asistente</c> de la configuración: <c>Asistente__MaxTurnos</c>…). El tope
/// diario de tokens por persona es aparte (<c>Anthropic__LimiteTokensPorUsuarioDia</c>).
/// </summary>
public sealed class clsLimitesAsistente
{
    /// <summary>Mensajes del usuario que admite una conversación; al llegar al límite hay que empezar otra.</summary>
    public int MaxTurnos { get; set; } = 20;

    public int MaxCaracteresMensaje { get; set; } = 2000;

    /// <summary>Veces seguidas que el modelo puede pedir herramientas antes de dar su respuesta.</summary>
    public int MaxIteracionesHerramientas { get; set; } = 6;

    public int MaxTokensRespuesta { get; set; } = 2048;

    /// <summary>Minutos que una acción propuesta espera la confirmación antes de vencer.</summary>
    public int VigenciaAccionMinutos { get; set; } = 30;

    /// <summary>Acciones que el asistente puede proponer en una misma respuesta.</summary>
    public int MaxAccionesPorRespuesta { get; set; } = 3;

    public int MaxTurnosEfectivo => Math.Clamp(MaxTurnos, 1, 200);
    public int MaxCaracteresMensajeEfectivo => Math.Clamp(MaxCaracteresMensaje, 100, 10_000);
    public int MaxIteracionesEfectivo => Math.Clamp(MaxIteracionesHerramientas, 1, 12);
    public int MaxTokensRespuestaEfectivo => Math.Clamp(MaxTokensRespuesta, 256, 8192);
    public TimeSpan VigenciaAccionEfectiva => TimeSpan.FromMinutes(Math.Clamp(VigenciaAccionMinutos, 1, 1440));
    public int MaxAccionesPorRespuestaEfectivo => Math.Clamp(MaxAccionesPorRespuesta, 1, 10);
}
