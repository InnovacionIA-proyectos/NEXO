using System.Net;
using System.Net.Mail;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Domain.Usuarios.Entidades;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Envío de correo por el SMTP corporativo. Configuración en la sección "Smtp"
/// (Host, Puerto, Usuario, Clave, Remitente, Ssl) — típicamente en
/// appsettings.{env}.local.json o variables de entorno. Sin configuración,
/// Configurado = false y quien llama decide cómo degradar.
/// </summary>
public sealed class SmtpCorreoAdapter(IConfiguration config, ILogger<SmtpCorreoAdapter> logger) : ICorreoPort
{
    public bool Configurado =>
        !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(config["Smtp:Remitente"]);

    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml,
        byte[]? adjunto, string? nombreAdjunto, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new InvalidOperationException("El SMTP corporativo no está configurado (sección Smtp).");

        using var mensaje = new MailMessage
        {
            From = new MailAddress(config["Smtp:Remitente"]!, "Nexo"),
            Subject = asunto,
            Body = cuerpoHtml,
            IsBodyHtml = true,
        };
        mensaje.To.Add(destinatario);

        if (adjunto is { Length: > 0 })
            mensaje.Attachments.Add(new Attachment(new MemoryStream(adjunto), nombreAdjunto ?? "cotizacion.pdf", "application/pdf"));

        using var smtp = new SmtpClient(config["Smtp:Host"], int.TryParse(config["Smtp:Puerto"], out var p) ? p : 587)
        {
            EnableSsl = !bool.TryParse(config["Smtp:Ssl"], out var ssl) || ssl,
        };
        var usuario = config["Smtp:Usuario"];
        if (!string.IsNullOrWhiteSpace(usuario))
            smtp.Credentials = new NetworkCredential(usuario, config["Smtp:Clave"]);

        await smtp.SendMailAsync(mensaje, ct);
        logger.LogInformation("Correo enviado a {Destinatario}: {Asunto}", destinatario, asunto);
    }
}
