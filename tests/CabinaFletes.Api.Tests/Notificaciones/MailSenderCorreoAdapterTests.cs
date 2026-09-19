using System.Net;
using System.Text;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Infrastructure.Notificaciones.Adaptadores;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CabinaFletes.Api.Tests.Notificaciones;

/// <summary>
/// El adaptador al microservicio de correo, contra un servidor de mentira.
///
/// LO QUE SE PRUEBA ES LA TRADUCCIÓN, que es todo lo que este adaptador hace: convertir lo
/// que el MailSender responde en algo que la política de reintentos del bus pueda tratar.
/// Y eso tiene una consecuencia medible: un 413 marcado como irreparable llega a la cola
/// de error al primer intento, mientras que marcado como pasajero se pasea noventa
/// segundos por la política antes de que alguien se entere.
///
/// También se afirma la forma del multipart, porque es un contrato con un servicio que no
/// controlamos: los destinatarios se repiten como campos "To", el adjunto va en
/// "Attachments" con nombre de archivo, y el cuerpo va marcado como HTML.
/// </summary>
public class MailSenderCorreoAdapterTests
{
    private const string BaseUrl = "http://mailsender.interno";

    /// <summary>Servidor de mentira: responde lo que se le diga y guarda lo que recibió.</summary>
    private sealed class ServidorFalso(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Recibida { get; private set; }
        public string? CuerpoRecibido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Recibida = request;
            CuerpoRecibido = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private static (MailSenderCorreoAdapter adaptador, ServidorFalso servidor) Armar(
        Func<HttpRequestMessage, HttpResponseMessage> responder, string? baseUrl = BaseUrl)
    {
        var servidor = new ServidorFalso(responder);
        var http = new HttpClient(servidor) { BaseAddress = new Uri(baseUrl ?? BaseUrl + "/") };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MailSenderCorreoAdapter.ClaveBaseUrl] = baseUrl,
                [MailSenderCorreoAdapter.ClaveRemitente] = "nexo@boterosoto.com.co",
            })
            .Build();
        return (new MailSenderCorreoAdapter(http, config, NullLogger<MailSenderCorreoAdapter>.Instance), servidor);
    }

    private static HttpResponseMessage Responder(HttpStatusCode codigo, string cuerpo = "") =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static CorreoParaEnviar Correo(bool conAdjunto = false) =>
        new(
            ["comercial@boterosoto.com.co"],
            "Cotización Nexo — Colanta",
            "<p>Hola</p>",
            conAdjunto ? [new AdjuntoDeCorreo("formato.pdf", [1, 2, 3])] : [],
            Metadatos: new Dictionary<string, string> { ["cotizacion"] = "abc" });

    [Fact]
    public async Task Un_202_devuelve_el_identificador_del_relay()
    {
        var (adaptador, _) = Armar(_ => Responder(
            HttpStatusCode.Accepted,
            """{"id":"7f3a","status":"Queued","statusUrl":"/api/v1/emails/7f3a"}"""));

        var id = await adaptador.EnviarAsync(Correo());

        Assert.Equal("7f3a", id);
    }

    [Fact]
    public async Task Un_202_sin_cuerpo_legible_no_invalida_el_encolado()
    {
        // Que el cuerpo no se pueda leer no deshace el 202: el correo ya fue aceptado. Se
        // pierde el identificador, no el correo — y tratarlo como fallo lo mandaría dos veces.
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.Accepted, "no es json"));

        var id = await adaptador.EnviarAsync(Correo());

        Assert.Null(id);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Los_rechazos_del_relay_son_irreparables(HttpStatusCode codigo)
    {
        // 400 mal formado, 413 adjunto demasiado grande, 422 no procesable: van a fallar
        // igual las cuatro veces. Marcados así, el bus no los reintenta.
        var (adaptador, _) = Armar(_ => Responder(codigo, """{"detail":"nope"}"""));

        await Assert.ThrowsAsync<CorreoRechazadoException>(() => adaptador.EnviarAsync(Correo()));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Los_errores_del_servidor_son_pasajeros(HttpStatusCode codigo)
    {
        var (adaptador, _) = Armar(_ => Responder(codigo));

        await Assert.ThrowsAsync<CorreoTransitorioException>(() => adaptador.EnviarAsync(Correo()));
    }

    [Fact]
    public async Task Si_el_relay_no_responde_es_pasajero()
    {
        // Servicio caído, red, DNS. Es exactamente el caso para el que existe la política
        // de reintentos.
        var (adaptador, _) = Armar(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<CorreoTransitorioException>(() => adaptador.EnviarAsync(Correo()));
        Assert.Contains("no respondió", ex.Message);
    }

    [Fact]
    public async Task Sin_URL_configurada_no_esta_configurado_y_no_se_reintenta()
    {
        // Reintentar por una configuración que falta solo retrasa el diagnóstico.
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.Accepted), baseUrl: null);

        Assert.False(adaptador.Configurado);
        await Assert.ThrowsAsync<CorreoRechazadoException>(() => adaptador.EnviarAsync(Correo()));
    }

    [Fact]
    public async Task El_multipart_tiene_la_forma_que_el_MailSender_espera()
    {
        // Es un contrato con un servicio que no controlamos: si alguien cambia un nombre
        // de campo, el correo deja de salir y el síntoma aparece lejos de la causa.
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.Accepted, """{"id":"x"}"""));

        await adaptador.EnviarAsync(Correo(conAdjunto: true));

        var cuerpo = servidor.CuerpoRecibido!;
        Assert.Equal(HttpMethod.Post, servidor.Recibida!.Method);
        Assert.Contains("api/v1/emails", servidor.Recibida.RequestUri!.ToString());
        Assert.Contains("multipart/form-data", servidor.Recibida.Content!.Headers.ContentType!.MediaType);

        Assert.Contains("name=To", cuerpo);
        Assert.Contains("comercial@boterosoto.com.co", cuerpo);
        Assert.Contains("name=From", cuerpo);
        Assert.Contains("name=Subject", cuerpo);
        Assert.Contains("name=Body", cuerpo);
        Assert.Contains("name=IsHtml", cuerpo);
        Assert.Contains("name=Metadata", cuerpo);
        Assert.Contains("name=Attachments", cuerpo);
        Assert.Contains("formato.pdf", cuerpo);
        Assert.Contains("application/pdf", cuerpo);
    }

    [Fact]
    public async Task Varios_destinatarios_se_repiten_como_campos_To()
    {
        // Así lo espera el multipart del MailSender para más de uno. Lo va a necesitar la
        // #22 cuando el destinatario salga de la sesión.
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.Accepted, """{"id":"x"}"""));

        await adaptador.EnviarAsync(new CorreoParaEnviar(
            ["uno@boterosoto.com.co", "dos@boterosoto.com.co"], "asunto", "<p>x</p>", []));

        var cuerpo = servidor.CuerpoRecibido!;
        Assert.Equal(2, cuerpo.Split("name=To").Length - 1);
        Assert.Contains("uno@boterosoto.com.co", cuerpo);
        Assert.Contains("dos@boterosoto.com.co", cuerpo);
    }

    [Fact]
    public async Task Los_metadatos_salen_como_JSON_valido()
    {
        // ESTA PRUEBA EXISTE POR UN FALLO REAL. Los metadatos iban como texto suelto
        // ("cotizacion=abc") y el MailSender rechazó el correo con 422:
        // "metadata must be a valid JSON document". Las pruebas contra este servidor
        // simulado no podían verlo, porque el simulado acepta cualquier cosa.
        //
        // Ahora el puerto recibe un diccionario y el adaptador serializa, así que un caso
        // de uso no puede equivocarse — y esto lo fija.
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.Accepted, """{"id":"x"}"""));

        await adaptador.EnviarAsync(new CorreoParaEnviar(
            ["uno@boterosoto.com.co"], "asunto", "<p>x</p>", [],
            Metadatos: new Dictionary<string, string> { ["cotizacion"] = "abc-123", ["origen"] = "nexo" }));

        var cuerpo = servidor.CuerpoRecibido!;
        var i = cuerpo.IndexOf("name=Metadata", StringComparison.Ordinal);
        Assert.True(i >= 0, "No se envió el campo Metadata.");

        // Se extrae el valor de esa parte del multipart y se comprueba que parsea.
        var desde = cuerpo.IndexOf("{", i, StringComparison.Ordinal);
        var hasta = cuerpo.IndexOf("}", desde, StringComparison.Ordinal);
        var json = cuerpo[desde..(hasta + 1)];

        var leido = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("abc-123", leido.RootElement.GetProperty("cotizacion").GetString());
        Assert.Equal("nexo", leido.RootElement.GetProperty("origen").GetString());
    }

    [Fact]
    public async Task Sin_metadatos_no_se_manda_el_campo()
    {
        // Mandar Metadata vacío o con una cadena vacía es justo lo que el relay rechaza.
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.Accepted, """{"id":"x"}"""));

        await adaptador.EnviarAsync(new CorreoParaEnviar(
            ["uno@boterosoto.com.co"], "asunto", "<p>x</p>", []));

        Assert.DoesNotContain("name=Metadata", servidor.CuerpoRecibido!);
    }
}
