using System.Net;
using System.Text;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.Common.Exceptions;
using Nexo.Infrastructure.Cubicacion.Adaptadores;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// S5-HU-18.1/#273: única llamada HTTP saliente de NEXO hacia Cubicador — contra un
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

    private static clsCrearRecomendacionInput clsSolicitud() => new(
        "LIN-001", [new clsMercanciaSolicitadaInput("tipo-1", 1, 100, 80, 60, 50)]);

    private const string RespuestaValida = """
        {"idRecomendacion":"REC-1","idLineaSolicitud":"LIN-001","origenSistema":"NEXO","resultado":"ASIGNADO",
         "motivoSinSeleccion":null,"condicionada":false,"mercanciasCondicionadas":[],"vehiculos":[],
         "advertencias":["sin candidatos ideales"],"extradimension":null}
        """;

    [Fact]
    public async Task Un_200_se_deserializa_tal_cual_lo_que_devuelve_Cubicador()
    {
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.OK, RespuestaValida));

        var resultado = await adaptador.CrearAsync(clsSolicitud());

        Assert.Equal("ASIGNADO", resultado.Resultado);
        Assert.Empty(resultado.Vehiculos);
        Assert.Contains("sin candidatos ideales", resultado.Advertencias);
        Assert.Equal(HttpMethod.Post, servidor.Recibida!.Method);
        Assert.EndsWith("recomendaciones", servidor.Recibida.RequestUri!.ToString());
    }

    // S5-HU-18.1/#273: SIN_SELECCION_AUTOMATICA y condicionada son RESULTADOS normales con
    // 200, no fallos — el adaptador no les da ningún tratamiento especial.
    [Fact]
    public async Task SIN_SELECCION_AUTOMATICA_llega_como_200_sin_lanzar_excepcion()
    {
        const string cuerpo = """
            {"idRecomendacion":"REC-2","idLineaSolicitud":"LIN-002","origenSistema":"NEXO",
             "resultado":"SIN_SELECCION_AUTOMATICA","motivoSinSeleccion":"SUPERA_K_MAX",
             "condicionada":false,"mercanciasCondicionadas":[],"vehiculos":[],"advertencias":[],"extradimension":null}
            """;
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.OK, cuerpo));

        var resultado = await adaptador.CrearAsync(clsSolicitud());

        Assert.Equal("SIN_SELECCION_AUTOMATICA", resultado.Resultado);
        Assert.Equal(MotivoSinSeleccion.SuperaKMax, resultado.MotivoSinSeleccion);
    }

    [Fact]
    public async Task El_cuerpo_enviado_usa_camelCase_como_espera_Cubicador()
    {
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.OK, RespuestaValida));

        await adaptador.CrearAsync(clsSolicitud());

        Assert.Contains("\"idLineaSolicitud\"", servidor.CuerpoRecibido);
        Assert.Contains("\"idTipoMercancia\"", servidor.CuerpoRecibido);
    }

    // S5-HU-18.1/#273: el código REAL que manda el Cubicador se propaga tal cual — ya no se
    // reemplaza por un código genérico inventado por NEXO.
    [Fact]
    public async Task Un_400_propaga_el_codigo_real_del_Cubicador()
    {
        var (adaptador, _) = Armar(_ => Responder(
            HttpStatusCode.BadRequest, """{"codigo":"MERCANCIA_DESCONOCIDA","mensaje":"'x' no está en el catálogo."}"""));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => adaptador.CrearAsync(clsSolicitud()));
        Assert.Equal("MERCANCIA_DESCONOCIDA", ex.Code);
        Assert.Equal("'x' no está en el catálogo.", ex.Message);
    }

    [Fact]
    public async Task Un_400_sin_cuerpo_parseable_cae_al_codigo_generico()
    {
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.BadRequest, "no es json"));

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => adaptador.CrearAsync(clsSolicitud()));
        Assert.Equal("CUBICACION_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Un_401_indica_credencial_desactualizada()
    {
        var (adaptador, _) = Armar(_ => Responder(HttpStatusCode.Unauthorized));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CrearAsync(clsSolicitud()));
        Assert.Equal("CUBICADOR_CREDENCIAL_INVALIDA", ex.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Los_errores_del_servidor_son_ServiceUnavailable(HttpStatusCode codigo)
    {
        var (adaptador, _) = Armar(_ => Responder(codigo));

        await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CrearAsync(clsSolicitud()));
    }

    [Fact]
    public async Task Si_Cubicador_no_responde_es_ServiceUnavailable()
    {
        var (adaptador, _) = Armar(_ => throw new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CrearAsync(clsSolicitud()));
        Assert.Contains("no respondió", ex.Message);
    }

    [Fact]
    public async Task Consultar_arma_la_query_con_idLineaSolicitud_y_origenSistema()
    {
        var (adaptador, servidor) = Armar(_ => Responder(HttpStatusCode.OK, RespuestaValida));

        await adaptador.ConsultarAsync("LIN-001", "COTIFLY");

        Assert.Equal(HttpMethod.Get, servidor.Recibida!.Method);
        var uri = servidor.Recibida.RequestUri!.ToString();
        Assert.Contains("idLineaSolicitud=LIN-001", uri);
        Assert.Contains("origenSistema=COTIFLY", uri);
    }

    [Fact]
    public async Task La_no_configurada_responde_503_accionable_sin_llamar_a_nadie()
    {
        var adaptador = new clsCubicacionRapidaNoConfiguradoAdapter();

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CrearAsync(clsSolicitud()));
        Assert.Equal("CUBICACION_RAPIDA_NO_CONFIGURADA", ex.Code);

        var ex2 = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.ConsultarAsync("LIN-001", "NEXO"));
        Assert.Equal("CUBICACION_RAPIDA_NO_CONFIGURADA", ex2.Code);
    }
}
