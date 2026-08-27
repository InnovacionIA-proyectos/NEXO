namespace CabinaFletes.Application.Common.Ports;

/// <summary>Envío de correo por el SMTP corporativo. Vive en Common porque lo
/// consume más de un módulo (Cotizaciones, Mensajería) — mismo criterio ya
/// aplicado a IAuditoriaPort/IPropuestaTarifaPort (ver MIGRACION.md).</summary>
public interface ICorreoPort
{
    bool Configurado { get; }

    Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml,
        byte[]? adjunto, string? nombreAdjunto, CancellationToken ct);
}
