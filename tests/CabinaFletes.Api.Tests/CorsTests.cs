using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

// Verifica S2-HU-02.2 (issue #8): el módulo del portal debe poder consumir la API
// desde su propio origen sin que el navegador bloquee la respuesta por CORS. La
// prueba no simula un navegador completo (HttpClient no aplica la política, solo la
// respeta quien la implementa) — lo que sí verifica, que es lo que decide un
// navegador real, es que el servidor devuelva (o no) el encabezado
// Access-Control-Allow-Origin según el Origin de la petición.
[Collection("api")]
public class CorsTests(ApiFixture fx)
{
    // Coincide con el valor por defecto de Program.cs (Cors:OrigenesPermitidos) para
    // no depender de configuración extra en el entorno de pruebas.
    private const string OrigenPermitidoPorDefecto = "http://localhost:5173";

    [Fact]
    public async Task Un_origen_de_la_lista_recibe_el_encabezado_de_CORS()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", OrigenPermitidoPorDefecto);

        var resp = await fx.Anon().SendAsync(request);

        Assert.True(resp.Headers.TryGetValues("Access-Control-Allow-Origin", out var valores));
        Assert.Equal(OrigenPermitidoPorDefecto, valores!.Single());
    }

    [Fact]
    public async Task Un_origen_fuera_de_la_lista_no_recibe_el_encabezado_de_CORS()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://origen-no-autorizado.example.com");

        var resp = await fx.Anon().SendAsync(request);

        Assert.False(resp.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
