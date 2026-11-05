using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Nexo.Application.Common.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Salida de correo por el microservicio MailSender, que recibe el adjunto por multipart y lo encola
/// (el relay SOAP solo adjunta archivos ya presentes en su ruta). No reintenta el SMTP: traduce la respuesta del
/// MailSender en excepciones que el bus sabe tratar. Sin URL configurada, Configurado = false y quien llama decide cómo degradar.</summary>
public sealed class clsMailSenderCorreoAdapter(
    HttpClient http,
    IConfiguration config,
    ILogger<clsMailSenderCorreoAdapter> log) : ICorreoPort
{
    /// <summary>URL base del microservicio.</summary>
    public const string ClaveBaseUrl = "MailSender:BaseUrl";

    /// <summary>Remitente del correo; opcional, el relay tiene su propio por defecto.</summary>
    public const string ClaveRemitente = "MailSender:Remitente";

    private const string clsRuta = "api/v1/emails";

    public bool Configurado => !string.IsNullOrWhiteSpace(config[ClaveBaseUrl]);

    public async Task<string?> EnviarAsync(clsCorreoParaEnviar correo, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new clsCorreoRechazadoException(
                $"La salida de correo no está configurada ({ClaveBaseUrl}). Reintentar no lo va a arreglar.");

        using var formulario = ArmarFormulario(correo);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.PostAsync(clsRuta, formulario, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // No respondió (red, servicio caído, tiempo agotado): caso para la política de reintentos del bus.
            throw new clsCorreoTransitorioException(
                $"El MailSender no respondió: {ex.Message}", ex);
        }

        using (respuesta)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

            if (respuesta.StatusCode == HttpStatusCode.Accepted)
            {
                var id = LeerIdentificador(cuerpo);
                log.LogInformation(
                    "Correo encolado en el MailSender ({Id}) para {Destinatarios} con {Adjuntos} adjunto(s).",
                    id ?? "sin id", string.Join(", ", correo.Destinatarios), correo.Adjuntos.Count);
                return id;
            }

            // 400/413/422: reintentar solo retrasa la alerta; el bus no reintenta clsCorreoRechazadoException y la lleva a la cola de error.
            if (respuesta.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.RequestEntityTooLarge
                or HttpStatusCode.UnprocessableEntity)
                throw new clsCorreoRechazadoException(
                    $"El MailSender rechazó el correo con {(int)respuesta.StatusCode}: {Recortar(cuerpo)}");

            throw new clsCorreoTransitorioException(
                $"El MailSender respondió {(int)respuesta.StatusCode}: {Recortar(cuerpo)}");
        }
    }

    private MultipartFormDataContent ArmarFormulario(clsCorreoParaEnviar correo)
    {
        var formulario = new MultipartFormDataContent();

        var remitente = config[ClaveRemitente];
        if (!string.IsNullOrWhiteSpace(remitente))
            formulario.Add(new StringContent(remitente), "From");

        // Los destinatarios se repiten como campos con el mismo nombre, como espera el multipart del MailSender.
        foreach (var destinatario in correo.Destinatarios)
            formulario.Add(new StringContent(destinatario), "To");

        formulario.Add(new StringContent(correo.Asunto), "Subject");
        formulario.Add(new StringContent(correo.CuerpoHtml), "Body");
        formulario.Add(new StringContent("true"), "IsHtml");

        // El relay EXIGE los metadatos como JSON válido: una cadena suelta se rechaza con 422.
        if (correo.Metadatos is { Count: > 0 })
            formulario.Add(new StringContent(JsonSerializer.Serialize(correo.Metadatos)), "Metadata");

        foreach (var adjunto in correo.Adjuntos)
        {
            var contenido = new ByteArrayContent(adjunto.Contenido);
            contenido.Headers.ContentType = new MediaTypeHeaderValue(TipoDe(adjunto.Nombre));
            formulario.Add(contenido, "Attachments", adjunto.Nombre);
        }

        return formulario;
    }

    /// <summary>El 202 trae {"id": "...", "status": "...", "statusUrl": "..."}.</summary>
    private static string? LeerIdentificador(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            using var json = JsonDocument.Parse(cuerpo);
            return json.RootElement.TryGetProperty("id", out var id) ? id.ToString() : null;
        }
        catch (JsonException)
        {
            // Un cuerpo ilegible no invalida el encolado: el correo ya fue aceptado, solo se pierde el identificador.
            return null;
        }
    }

    private static string TipoDe(string nombre) =>
        nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
        : nombre.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        : "application/octet-stream";

    private static string Recortar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? "(sin cuerpo)"
        : texto.Length <= 400 ? texto.Trim()
        : texto[..400].Trim() + "…";
}
