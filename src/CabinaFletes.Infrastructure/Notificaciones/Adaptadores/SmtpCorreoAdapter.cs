using System.Net;
using System.Net.Mail;
using CabinaFletes.Application.Common.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Salida de correo por SMTP directo. Configuración en la sección "Smtp".
///
/// ES EL CAMINO DE RESERVA, no el previsto. Se usa solo cuando no hay
/// <c>MailSender:BaseUrl</c> configurada, para que un entorno que ya tenía el SMTP puesto
/// no se quede sin notificación al entrar el microservicio.
///
/// Y hay una razón concreta para preferir el otro: el relay corporativo por SOAP solo
/// adjunta archivos que ya están en una ruta alcanzable por ese servidor, y el formato de
/// la cotización llega como archivo en la petición — no está en ningún disco compartido.
/// Este adaptador adjunta desde memoria porque habla SMTP directo, pero es el camino que
/// la compañía está dejando atrás.
///
/// Sube <see cref="CorreoTransitorioException"/> en vez de la excepción cruda de SMTP para
/// que el bus la trate igual que la del microservicio: la política de reintentos no tiene
/// por qué saber cuál adaptador está detrás.
/// </summary>
public sealed class SmtpCorreoAdapter(IConfiguration config, ILogger<SmtpCorreoAdapter> logger) : ICorreoPort
{
    public bool Configurado =>
        !string.IsNullOrWhiteSpace(config["Smtp:Host"]) &&
        !string.IsNullOrWhiteSpace(config["Smtp:Remitente"]);

    public async Task<string?> EnviarAsync(CorreoParaEnviar correo, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new CorreoRechazadoException(
                "El SMTP corporativo no está configurado (sección Smtp). Reintentar no lo va a arreglar.");

        if (correo.Destinatarios.Count == 0)
            throw new CorreoRechazadoException("El correo no tiene destinatarios.");

        using var mensaje = new MailMessage
        {
            From = new MailAddress(config["Smtp:Remitente"]!, "Nexo"),
            Subject = correo.Asunto,
            Body = correo.CuerpoHtml,
            IsBodyHtml = true,
        };
        foreach (var destinatario in correo.Destinatarios)
            mensaje.To.Add(destinatario);

        foreach (var adjunto in correo.Adjuntos)
            mensaje.Attachments.Add(new Attachment(
                new MemoryStream(adjunto.Contenido), adjunto.Nombre, "application/pdf"));

        using var smtp = new SmtpClient(
            config["Smtp:Host"], int.TryParse(config["Smtp:Puerto"], out var p) ? p : 587)
        {
            EnableSsl = !bool.TryParse(config["Smtp:Ssl"], out var ssl) || ssl,
        };
        var usuario = config["Smtp:Usuario"];
        if (!string.IsNullOrWhiteSpace(usuario))
            smtp.Credentials = new NetworkCredential(usuario, config["Smtp:Clave"]);

        try
        {
            await smtp.SendMailAsync(mensaje, ct);
        }
        catch (SmtpFailedRecipientException ex)
        {
            // El destinatario no existe o el servidor lo rechaza: reintentarlo no cambia nada.
            throw new CorreoRechazadoException($"El SMTP rechazó el destinatario: {ex.Message}", ex);
        }
        catch (SmtpException ex)
        {
            throw new CorreoTransitorioException($"El SMTP falló: {ex.Message}", ex);
        }

        logger.LogInformation(
            "Correo enviado por SMTP directo a {Destinatarios}: {Asunto}",
            string.Join(", ", correo.Destinatarios), correo.Asunto);

        // El SMTP directo no da identificador con el que consultar después.
        return null;
    }
}
