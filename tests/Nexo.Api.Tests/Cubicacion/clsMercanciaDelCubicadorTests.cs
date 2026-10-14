using System.Net;
using System.Text;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>La elección de la mercancía de las pruebas E2E del Cubicador real: usa la semilla local si
/// está y, si no, la primera mercancía activa y cubicable del catálogo real. Corre sin Cubicador.</summary>
public class clsMercanciaDelCubicadorTests
{
    private sealed class clsRespuestaFija(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private static HttpClient Cliente(string json) => new(new clsRespuestaFija(json)) { BaseAddress = new Uri("http://cubicador.test") };

    [Fact]
    public async Task Si_la_semilla_esta_y_es_usable_se_usa_y_se_marca_como_semilla()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"otra","activo":true,"cubicable":true},{"id":"m-CAJAS_CARTON","activo":true,"cubicable":true,"condicionadaCubicacion":false}]
            """));

        Assert.Equal(("m-CAJAS_CARTON", true), (m.Id, m.EsLaDeLaSemilla));
    }

    [Fact]
    public async Task Sin_la_semilla_toma_la_primera_activa_cubicable_y_no_condicionada()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"inactiva","activo":false,"cubicable":true},
             {"id":"no-cubicable","activo":true,"cubicable":false},
             {"id":"condicionada","activo":true,"cubicable":true,"condicionadaCubicacion":true},
             {"id":"buena","activo":true,"cubicable":true,"condicionadaCubicacion":false}]
            """));

        Assert.Equal(("buena", false), (m.Id, m.EsLaDeLaSemilla));
    }

    [Fact]
    public async Task Una_semilla_inactiva_no_se_usa()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"m-CAJAS_CARTON","activo":false,"cubicable":true},{"id":"x","activo":true,"cubicable":true}]
            """));

        Assert.Equal(("x", false), (m.Id, m.EsLaDeLaSemilla));
    }

    [Fact]
    public async Task Un_catalogo_sin_mercancias_usables_falla_con_un_mensaje_claro()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            clsMercanciaDelCubicador.ElegirAsync(Cliente("""[{"id":"a","activo":false,"cubicable":true}]""")));

        Assert.Contains("ninguna mercancía activa y cubicable", ex.Message);
    }
}
