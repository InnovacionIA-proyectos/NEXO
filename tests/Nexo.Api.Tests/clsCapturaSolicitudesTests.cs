using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsCapturaSolicitudesTests
{
    private readonly clsApiFixture _fx;
    public clsCapturaSolicitudesTests(clsApiFixture fx) => _fx = fx;

    // S4-HU-18.10/#227: notas/observaciones comerciales — de creación a lectura.
    [Fact]
    public async Task Notas_capturadas_se_leen_de_vuelta_al_abrir_la_solicitud()
    {
        var crear = await _fx.Client().PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(),
            tipo = "spot",
            notas = "Entregar solo en horario diurno, contactar a portería con 1h de anticipación.",
            lineas = new[] { new { origen = "Medellín", destino = "Bogotá", peso = 10000 } },
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Json()).GetProperty("id").GetGuid();

        var costeo = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{id}");

        Assert.Equal("Entregar solo en horario diurno, contactar a portería con 1h de anticipación.",
            costeo.GetProperty("solicitud").GetProperty("notas").GetString());
    }

    [Fact] // sin notas: no revienta, queda null.
    public async Task Sin_notas_la_solicitud_se_crea_y_lee_con_notas_nulas()
    {
        var crear = await _fx.Client().PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(),
            tipo = "spot",
            lineas = new[] { new { origen = "Medellín", destino = "Bogotá", peso = 10000 } },
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Json()).GetProperty("id").GetGuid();

        var costeo = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{id}");

        Assert.Equal(JsonValueKind.Null, costeo.GetProperty("solicitud").GetProperty("notas").ValueKind);
    }
}
