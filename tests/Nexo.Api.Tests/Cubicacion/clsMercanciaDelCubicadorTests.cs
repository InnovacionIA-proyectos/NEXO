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
            [{"id":"m-OTRA","codigoMercancia":"OTRA","activo":true,"cubicable":true},
             {"id":"m-CAJAS_CARTON","codigoMercancia":"CAJAS_CARTON","activo":true,"cubicable":true,"condicionadaCubicacion":false}]
            """));

        Assert.Equal(("CAJAS_CARTON", true), (m.Codigo, m.EsLaDeLaSemilla));   // el CÓDIGO, no el id (S5-HU-17.7)
    }

    [Fact]
    public async Task Sin_la_semilla_toma_la_primera_activa_cubicable_y_no_condicionada()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"1","codigoMercancia":"INACTIVA","activo":false,"cubicable":true},
             {"id":"2","codigoMercancia":"NO_CUBICABLE","activo":true,"cubicable":false},
             {"id":"3","codigoMercancia":"CONDICIONADA","activo":true,"cubicable":true,"condicionadaCubicacion":true},
             {"id":"4","codigoMercancia":"BUENA","activo":true,"cubicable":true,"condicionadaCubicacion":false}]
            """));

        Assert.Equal(("BUENA", false), (m.Codigo, m.EsLaDeLaSemilla));
    }

    [Fact]
    public async Task Una_semilla_inactiva_no_se_usa()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"m-CAJAS_CARTON","codigoMercancia":"CAJAS_CARTON","activo":false,"cubicable":true},
             {"id":"x","codigoMercancia":"X","activo":true,"cubicable":true}]
            """));

        Assert.Equal(("X", false), (m.Codigo, m.EsLaDeLaSemilla));
    }

    [Fact]
    public async Task Una_fila_sin_codigoMercancia_no_se_puede_usar_aunque_tenga_id()
    {
        var m = await clsMercanciaDelCubicador.ElegirAsync(Cliente("""
            [{"id":"solo-id","activo":true,"cubicable":true},
             {"id":"2","codigoMercancia":"CON_CODIGO","activo":true,"cubicable":true}]
            """));

        Assert.Equal("CON_CODIGO", m.Codigo);
    }

    [Fact]
    public async Task Un_catalogo_sin_mercancias_usables_falla_con_un_mensaje_claro()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            clsMercanciaDelCubicador.ElegirAsync(Cliente("""[{"id":"a","codigoMercancia":"A","activo":false,"cubicable":true}]""")));

        Assert.Contains("ninguna mercancía activa y cubicable", ex.Message);
    }
}
