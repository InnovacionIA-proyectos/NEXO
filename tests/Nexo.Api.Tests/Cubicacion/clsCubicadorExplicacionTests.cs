using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Infrastructure.Cubicacion.Adaptadores;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// La ruta de NEXO que reenvía al Cubicador la explicación de la recomendación: el adaptador contra un servidor de mentira (cualquier falla es
/// null, nunca una excepción) y la ruta contra la API real (roles, parámetros y que siempre sea 200).
/// </summary>
public class clsCubicadorExplicacionAdapterTests
{
    private sealed class clsServidorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Recibida { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Recibida = request;
            return Task.FromResult(responder(request));
        }
    }

    private static (clsCubicadorExplicacionAdapter, clsServidorFalso) Armar(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var servidor = new clsServidorFalso(responder);
        return (new clsCubicadorExplicacionAdapter(new HttpClient(servidor) { BaseAddress = new Uri("http://cubicador.interno/") },
            NullLogger<clsCubicadorExplicacionAdapter>.Instance), servidor);
    }

    private static HttpResponseMessage Json(HttpStatusCode codigo, string cuerpo) =>
        new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Devuelve_el_texto_que_genera_el_Cubicador_y_pide_la_linea_y_el_origen()
    {
        var (adaptador, servidor) = Armar(_ => Json(HttpStatusCode.OK, """{"explicacion":"  Se eligió un tractocamión porque la carga llena el 82 % del volumen.  "}"""));

        var texto = await adaptador.ObtenerAsync("LIN 001/A", "NEXO");

        Assert.Equal("Se eligió un tractocamión porque la carga llena el 82 % del volumen.", texto);
        Assert.Equal(HttpMethod.Get, servidor.Recibida!.Method);
        Assert.Equal("http://cubicador.interno/recomendaciones/explicacion?idLineaSolicitud=LIN%20001%2FA&origenSistema=NEXO", servidor.Recibida.RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData("""{"explicacion":null}""")]
    [InlineData("""{"explicacion":""}""")]
    [InlineData("""{"explicacion":"   "}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("esto no es json")]
    [InlineData("[]")]
    public async Task Sin_texto_o_con_una_respuesta_que_no_se_entiende_devuelve_null(string cuerpo)
    {
        var (adaptador, _) = Armar(_ => Json(HttpStatusCode.OK, cuerpo));

        Assert.Null(await adaptador.ObtenerAsync("LIN-1", "NEXO"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Un_error_del_Cubicador_devuelve_null_y_no_una_excepcion(HttpStatusCode codigo)
    {
        var (adaptador, _) = Armar(_ => Json(codigo, """{"codigo":"X","mensaje":"y"}"""));

        Assert.Null(await adaptador.ObtenerAsync("LIN-1", "NEXO"));
    }

    [Fact]
    public async Task Con_el_Cubicador_caido_o_sin_responder_devuelve_null()
    {
        var (caido, _) = Armar(_ => throw new HttpRequestException("conexión rechazada"));
        var (lento, _) = Armar(_ => throw new TaskCanceledException("tiempo agotado"));

        Assert.Null(await caido.ObtenerAsync("LIN-1", "NEXO"));
        Assert.Null(await lento.ObtenerAsync("LIN-1", "NEXO"));
    }

    [Fact]
    public async Task Si_quien_llama_cancela_la_cancelacion_se_propaga()
    {
        var (adaptador, _) = Armar(_ => throw new TaskCanceledException());
        using var cancelada = new CancellationTokenSource();
        await cancelada.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => adaptador.ObtenerAsync("LIN-1", "NEXO", cancelada.Token));
    }

    [Fact]
    public async Task El_adaptador_sin_configurar_devuelve_null()
    {
        Assert.Null(await new clsCubicadorExplicacionNoConfiguradoAdapter().ObtenerAsync("LIN-1", "NEXO"));
    }
}

[Collection("api")]
public class clsCubicacionExplicacionApiTests(clsApiFixture fixture)
{
    private const string Ruta = "/api/cubicacion/explicacion";

    private sealed class PuertoFalso(string? texto) : ICubicadorExplicacionPort
    {
        public (string Linea, string Origen)? Recibido { get; private set; }

        public Task<string?> ObtenerAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default)
        {
            Recibido = (idLineaSolicitud, origenSistema);
            return Task.FromResult(texto);
        }
    }

    private HttpClient ClienteCon(PuertoFalso puerto, string rol)
    {
        var c = fixture.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ICubicadorExplicacionPort>();
            s.AddSingleton<ICubicadorExplicacionPort>(puerto);
        })).CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminBiblioteca")]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task Los_roles_de_la_recomendacion_reciben_el_texto_con_200(string rol)
    {
        var puerto = new PuertoFalso("Se eligió esa flota por el peso.");
        using var c = ClienteCon(puerto, rol);

        var resp = await c.GetAsync($"{Ruta}?idLineaSolicitud=LIN-9&origenSistema=NEXO");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("Se eligió esa flota por el peso.", (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("explicacion").GetString());
        Assert.Equal(("LIN-9", "NEXO"), puerto.Recibido);
    }

    [Fact]
    public async Task Sin_texto_responde_200_con_explicacion_null()
    {
        using var c = ClienteCon(new PuertoFalso(null), "Comercial");

        var resp = await c.GetAsync($"{Ruta}?idLineaSolicitud=LIN-9&origenSistema=NEXO");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("explicacion").ValueKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?idLineaSolicitud=LIN-9")]
    [InlineData("?origenSistema=NEXO")]
    [InlineData("?idLineaSolicitud=%20&origenSistema=NEXO")]
    public async Task Sin_idLineaSolicitud_u_origenSistema_es_400_PARAMETROS_REQUERIDOS(string consulta)
    {
        var puerto = new PuertoFalso("no debe llamarse");
        using var c = ClienteCon(puerto, "Comercial");

        var resp = await c.GetAsync(Ruta + consulta);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("PARAMETROS_REQUERIDOS", await resp.ErrorCode());
        Assert.Null(puerto.Recibido);
    }

    [Theory]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task Los_roles_sin_permiso_reciben_403(string rol)
    {
        var puerto = new PuertoFalso("no debe llamarse");
        using var c = ClienteCon(puerto, rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"{Ruta}?idLineaSolicitud=LIN-9&origenSistema=NEXO")).StatusCode);
        Assert.Null(puerto.Recibido);
    }

    [Fact]
    public async Task Sin_sesion_recibe_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Anon().GetAsync($"{Ruta}?idLineaSolicitud=LIN-9&origenSistema=NEXO")).StatusCode);
    }

    [Fact]
    public async Task Con_el_Cubicador_sin_configurar_responde_200_con_explicacion_null()
    {
        // Sin Cubicador3D:* la DI registra el adaptador sin configurar; con ellas, el real contra una línea que no existe también da null.
        var resp = await fixture.Client("Comercial").GetAsync($"{Ruta}?idLineaSolicitud=NO-EXISTE-{Guid.NewGuid():N}&origenSistema=NEXO");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("explicacion").ValueKind);
    }
}
