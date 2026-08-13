using System.Net;
using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

[Collection("api")]
public class MetricasTests
{
    private readonly ApiFixture _fx;
    public MetricasTests(ApiFixture fx) => _fx = fx;

    // 1) Happy path (rol Líder)
    [Fact]
    public async Task Cumplimiento_devuelve_kpis()
    {
        var resp = await _fx.Client("LiderProceso").GetAsync("/api/metricas/cumplimiento");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Json();
        Assert.True(json.GetProperty("totalSolicitudes").GetInt32() > 0);
        Assert.True(json.TryGetProperty("cumplimientoSla", out _));
    }

    // 3) Autenticación
    [Fact]
    public async Task Cumplimiento_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync("/api/metricas/cumplimiento");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 5) Edge — permiso: el coordinador no ve el tablero del líder -> 403
    [Fact]
    public async Task Cumplimiento_con_rol_no_autorizado_devuelve_403()
    {
        var resp = await _fx.Client("CoordinadorFletes").GetAsync("/api/metricas/cumplimiento");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
