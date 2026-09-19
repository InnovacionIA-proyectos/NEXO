using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

[Collection("api")]
public class TarifasHistoricasTests
{
    private readonly ApiFixture _fx;
    public TarifasHistoricasTests(ApiFixture fx) => _fx = fx;

    // 1) Happy path — búsqueda libre en la Biblioteca de Tarifas Ganadas
    [Fact]
    public async Task Buscar_por_texto_devuelve_resultados()
    {
        // Issue #166: con 'q' (rama "buscar") la respuesta ahora es { items, total }.
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/tarifas-historicas?q=Medell%C3%ADn");
        var items = pagina.GetProperty("items");
        Assert.True(pagina.GetProperty("total").GetInt32() > 0);
        Assert.True(items.GetArrayLength() > 0);
        foreach (var it in items.EnumerateArray())
            Assert.Contains("Medell", it.GetProperty("ruta").GetString());
    }

    // 3) Autenticación
    [Fact]
    public async Task Buscar_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync("/api/tarifas-historicas?q=Bogota");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 5) Edge cases
    [Fact] // sin 'q' -> sugerencias (no error)
    public async Task Sin_q_devuelve_sugerencias()
    {
        var resp = await _fx.Client().GetAsync("/api/tarifas-historicas");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] // cada ítem trae su semáforo de vigencia
    public async Task Items_incluyen_semaforo_de_vigencia()
    {
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/tarifas-historicas?q=Bogot%C3%A1");
        foreach (var it in pagina.GetProperty("items").EnumerateArray())
        {
            var v = it.GetProperty("vigencia").GetString();
            Assert.Contains(v, new[] { "vigente", "por_vencer", "vencida" });
        }
    }
}
