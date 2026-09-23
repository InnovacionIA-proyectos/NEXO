using System.Net;
using System.Text;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.Common.Exceptions;
using Nexo.Infrastructure.Cubicacion.Adaptadores;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// [S3-HU-04.3, issue #78] Única llamada HTTP saliente de NEXO hacia Cubicador — contra un
/// servidor de mentira, igual criterio que clsMailSenderCorreoAdapterTests: lo que se prueba
/// es la traducción de la respuesta, que es todo lo que este adaptador hace.
/// </summary>
public class clsCubicacionRapidaAdapterTests
{
    private const string BaseUrl = "http://cubicador.interno/";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class clsServidorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Recibida { get; private set; }
        public string? CuerpoRecibido { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Recibida = request;
            CuerpoRecibido = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return responder(request);
        }
    }

    private static (clsCubicacionRapidaAdapter adaptador, clsServidorFalso servidor) Armar(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var servidor = new clsServidorFalso(responder);
        var http = new HttpClient(servidor) { BaseAddress = new Uri(BaseUrl) };
        return (new clsCubicacionRapidaAdapter(http, NullLogger<clsCubicacionRapidaAdapter>.Instance), servidor);
    }

    private static HttpResponseMessage Responder(HttpStatusCode codigo, string cuerpo = "") =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static clsCubicacionRapidaSolicitud clsSolicitud() => new(
        [new clsItemCubicacionRapidaDto("Caja", 1, 100, 80, 60, 50, null)], null, null);

    private const string RespuestaValida = """
        {"matrices":[],"sugerencia":null,"advertencias":["sin unidades candidatas"],"sugerenciaFlota":null,"extradimension":null}
        """;

    [Fact]
    public async Task Un_200_se_deserializa_tal_cual_lo_que_devuelve_Cubicador()
    {
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.OK, RespuestaValida));

        var resultado = await adaptador.EvaluarAsync(clsSolicitud());

        Assert.Empty(resultado.Matrices);
        Assert.Null(resultado.Sugerencia);
        Assert.Contains("sin unidades candidatas", resultado.Advertencias);
        Assert.Equal(HttpMethod.Post, servidor.Recibida!.Method);
        Assert.EndsWith("api/cubicacion", servidor.Recibida.RequestUri!.ToString());
    }

    [Fact]
    public async Task El_cuerpo_enviado_usa_camelCase_como_espera_Cubicador()
    {
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.OK, RespuestaValida));

        await adaptador.EvaluarAsync(clsSolicitud());

        Assert.Contains("\"largoCm\"", servidor.CuerpoRecibido);
        Assert.Contains("\"tipoMercancia\"", servidor.CuerpoRecibido);
    }

    [Fact]
    public async Task Un_400_se_traduce_a_ValidationFailedException_no_a_ServiceUnavailable()
    {
        // La culpa es del request (medidas/ítems inválidos), no del transporte — reintentar
        // esto cuatro veces no lo va a arreglar.
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.BadRequest, """{"codigo":"MEDIDAS_INVALIDAS"}"""));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => adaptador.EvaluarAsync(clsSolicitud()));
        Assert.Equal("CUBICACION_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Un_401_indica_credencial_desactualizada()
    {
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.EvaluarAsync(clsSolicitud()));
        Assert.Equal("CUBICADOR_CREDENCIAL_INVALIDA", ex.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Los_errores_del_servidor_son_ServiceUnavailable(HttpStatusCode codigo)
    {
        var (adaptador, _) = Armar(_ => Responder(codigo));

        await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.EvaluarAsync(clsSolicitud()));
    }

    [Fact]
    public async Task Si_Cubicador_no_responde_es_ServiceUnavailable()
    {
        var (adaptador, _) = Armar(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.EvaluarAsync(clsSolicitud()));
        Assert.Contains("no respondió", ex.Message);
    }

    [Fact]
    public async Task La_no_configurada_responde_503_accionable_sin_llamar_a_nadie()
    {
        var adaptador = new clsCubicacionRapidaNoConfiguradoAdapter();

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.EvaluarAsync(clsSolicitud()));
        Assert.Equal("CUBICACION_RAPIDA_NO_CONFIGURADA", ex.Code);
    }
}
