using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Nexo.Api.Tests.PlanDeCargue;

/// <summary>
/// Issue #183: con el valor por defecto real de la compuerta de paridad (cadena vacía
/// en Cubicacion:ParidadVerificada, no ausente), POST .../optimizar debe responder 503
/// documentado, no un 500 no controlado. Causa: clsParidadCubicadorAdapter.Habilitada usaba
/// config.GetValue&lt;bool&gt;(...), que lanza FormatException con "" en vez de tratarla
/// como "no habilitada" — reemplazado por bool.TryParse(...).
/// </summary>
[Collection("api")]
public class clsOptimizarPlanCargueControllerTests(clsApiFixture fixture)
{
    private HttpClient ClienteConCompuertaEnBlanco(string valorParidadVerificada)
    {
        var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cubicacion:ParidadVerificada"] = valorParidadVerificada,
                })));
        var c = factory.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, "Admin");
        return c;
    }

    [Fact]
    public async Task Con_la_compuerta_en_cadena_vacia_responde_503_no_500()
    {
        // La reproducción exacta del bug: no es que falte la clave, es que existe con "".
        using var client = ClienteConCompuertaEnBlanco("");

        var resp = await client.PostAsync(
            $"/api/planes-cargue/{Guid.NewGuid()}/optimizar", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        var cuerpo = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("CUBICACION_EN_VERIFICACION", cuerpo?["code"]?.ToString());
    }

    [Fact]
    public async Task Con_la_compuerta_en_false_tambien_responde_503()
    {
        using var client = ClienteConCompuertaEnBlanco("false");

        var resp = await client.PostAsync(
            $"/api/planes-cargue/{Guid.NewGuid()}/optimizar", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }
}
