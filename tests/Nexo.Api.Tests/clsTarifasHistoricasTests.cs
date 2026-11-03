using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.BibliotecaTarifas.Entidades;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsTarifasHistoricasTests
{
    private readonly clsApiFixture _fx;
    public clsTarifasHistoricasTests(clsApiFixture fx) => _fx = fx;

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

    // 6) S4-HU-13.1/#219 — corte de 3 meses obligatorio en la sugerencia automática.
    [Fact]
    public async Task Sugerencia_por_ruta_y_vehiculo_excluye_tarifas_de_mas_de_3_meses()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var (rutaId, vehiculoId, idVieja, idReciente) = _fx.WithDb(db =>
        {
            var ruta = new clsRuta { Origen = $"Origen {sufijo}", Destino = $"Destino {sufijo}" };
            var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = $"Tipo {sufijo}" };
            db.Rutas.Add(ruta);
            db.ConfiguracionesVehiculo.Add(vehiculo);

            var vieja = new clsTarifaHistorica { RutaId = ruta.Id, ConfigVehiculoId = vehiculo.Id, Valor = 1_000_000m, Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-4)) };
            var reciente = new clsTarifaHistorica { RutaId = ruta.Id, ConfigVehiculoId = vehiculo.Id, Valor = 2_000_000m, Fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)) };
            db.TarifasHistoricas.AddRange(vieja, reciente);
            db.SaveChanges();
            return (ruta.Id, vehiculo.Id, vieja.Id, reciente.Id);
        });

        var items = await _fx.Client().GetFromJsonAsync<JsonElement[]>($"/api/tarifas-historicas?ruta={rutaId}&vehiculo={vehiculoId}");

        var ids = items!.Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(idVieja, ids); // más de 3 meses: excluida
        Assert.Contains(idReciente, ids);    // dentro del corte: visible
    }
}
