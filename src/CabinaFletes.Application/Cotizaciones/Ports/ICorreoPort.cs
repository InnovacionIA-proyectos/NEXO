namespace CabinaFletes.Application.Cotizaciones.Ports;

/// <summary>Envío de correo por el SMTP corporativo.</summary>
public interface ICorreoPort
{
    bool Configurado { get; }

    Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml,
        byte[]? adjunto, string? nombreAdjunto, CancellationToken ct);
}
