using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CabinaFletes.Application.Common.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Salida de correo por el microservicio MailSender de la compañía.
///
/// POR QUÉ NO SMTP DIRECTO. El servicio SOAP corporativo solo adjunta archivos que ya
/// están en una ruta alcanzable por ese servidor, y el formato de la cotización llega
/// como archivo en la petición: no está en ningún disco compartido. El MailSender existe
/// justamente para eso — recibe el adjunto por multipart y lo encola.
///
/// DOS COLAS, Y CONVIENE NO CONFUNDIRLAS. El MailSender tiene su propia cola durable y su
/// worker: cuando responde 202, se compromete a intentar el envío al relay y a
/// reintentarlo. Nuestra cola garantiza otra cosa distinta: que la notificación LE LLEGUE
/// al MailSender aunque esté caído cuando se entregó la cotización. Por eso este
/// adaptador no reintenta el SMTP —eso no es asunto suyo— sino que traduce lo que el
/// MailSender responde en algo que nuestro bus pueda tratar.
///
/// LA URL VA EN CONFIGURACIÓN porque cambia en cada despliegue mientras el servicio hace
/// lo mismo. Sin URL configurada, Configurado = false y quien llama decide cómo degradar;
/// no se inventa un valor por defecto que apunte a un entorno equivocado.
/// </summary>
public sealed class MailSenderCorreoAdapter(
    HttpClient http,
    IConfiguration config,
    ILogger<MailSenderCorreoAdapter> log) : ICorreoPort
{
    /// <summary>URL base del microservicio. Cambia por despliegue.</summary>
    public const string ClaveBaseUrl = "MailSender:BaseUrl";

    /// <summary>Remitente con el que sale el correo. Opcional: el relay tiene su propio por defecto.</summary>
    public const string ClaveRemitente = "MailSender:Remitente";

    private const string Ruta = "api/v1/emails";

    public bool Configurado => !string.IsNullOrWhiteSpace(config[ClaveBaseUrl]);

    public async Task<string?> EnviarAsync(CorreoParaEnviar correo, CancellationToken ct = default)
    {
        if (!Configurado)
            throw new CorreoRechazadoException(
                $"La salida de correo no está configurada ({ClaveBaseUrl}). Reintentar no lo va a arreglar.");

        using var formulario = ArmarFormulario(correo);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.PostAsync(Ruta, formulario, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            // No respondió: red, servicio caído, tiempo agotado. Es exactamente el caso
            // para el que existe la política de reintentos del bus.
            throw new CorreoTransitorioException(
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

            // 400 mal formado, 413 adjunto demasiado grande, 422 no procesable: reintentar
            // esto cuatro veces solo retrasa la alerta. El bus está configurado para no
            // reintentar CorreoRechazadoException y llevarla derecho a la cola de error.
            if (respuesta.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.RequestEntityTooLarge
                or HttpStatusCode.UnprocessableEntity)
                throw new CorreoRechazadoException(
                    $"El MailSender rechazó el correo con {(int)respuesta.StatusCode}: {Recortar(cuerpo)}");

            throw new CorreoTransitorioException(
                $"El MailSender respondió {(int)respuesta.StatusCode}: {Recortar(cuerpo)}");
        }
    }

    private MultipartFormDataContent ArmarFormulario(CorreoParaEnviar correo)
    {
        var formulario = new MultipartFormDataContent();

        var remitente = config[ClaveRemitente];
        if (!string.IsNullOrWhiteSpace(remitente))
            formulario.Add(new StringContent(remitente), "From");

        // Los destinatarios se repiten como campos con el mismo nombre: así lo espera el
        // multipart del MailSender para varios.
        foreach (var destinatario in correo.Destinatarios)
            formulario.Add(new StringContent(destinatario), "To");

        formulario.Add(new StringContent(correo.Asunto), "Subject");
        formulario.Add(new StringContent(correo.CuerpoHtml), "Body");
        formulario.Add(new StringContent("true"), "IsHtml");

        // El relay EXIGE que los metadatos sean un documento JSON válido: una cadena
        // suelta se rechaza con 422. Serializar aquí es lo correcto —el formato es asunto
        // del transporte— y de paso hace imposible que un caso de uso se equivoque.
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
            // Que el cuerpo del 202 no se pueda leer no invalida el encolado: el correo ya
            // fue aceptado. Se pierde el identificador, no el correo.
            return null;
        }
    }

    private static string TipoDe(string nombre) =>
        nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
        : nombre.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        : "application/octet-stream";

    /// <summary>El detalle del error va al log y al mensaje de la excepción; sin volcar páginas.</summary>
    private static string Recortar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? "(sin cuerpo)"
        : texto.Length <= 400 ? texto.Trim()
        : texto[..400].Trim() + "…";
}
