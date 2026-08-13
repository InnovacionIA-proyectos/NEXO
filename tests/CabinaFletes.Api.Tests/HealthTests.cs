using System.Net;
using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

[Collection("api")]
public class HealthTests
{
    private readonly ApiFixture _fx;
    public HealthTests(ApiFixture fx) => _fx = fx;

    [Fact] // anónimo permitido + verifica la BD
    public async Task Health_responde_200_y_healthy()
    {
        var resp = await _fx.Anon().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Json();
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
    }
}
