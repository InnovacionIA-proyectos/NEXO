using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

namespace Nexo.Api.Tests;

/// <summary>
/// <c>POST /api/solicitudes/recomendar-vehiculo-contenedor</c> contra la API real, con la tabla de rangos de mentira para no depender de lo
/// que tenga la base: quién lo usa, la respuesta por lote en el mismo orden y las validaciones.
/// </summary>
[Collection("api")]
public class clsRecomendarVehiculoContenedorApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/solicitudes/recomendar-vehiculo-contenedor";

    private sealed class TablaDeMentira : IRangosPesoContenedorPort
    {
        private static readonly clsRangoPesoContenedorDto[] Filas = [new(20, "2S2", 24_000), new(40, "2S3", 30_000), new(40, "3S3", 34_000)];

        public Task<clsVehiculoDeContenedor> VehiculoParaAsync(int contenedorPies, decimal pesoKg, CancellationToken ct = default) =>
            Task.FromResult(new clsVehiculoDeContenedor(true, clsRecomendadorDeVehiculoDeContenedor.Elegir(Filas, contenedorPies, pesoKg)));

        public Task<int?> PesoMaximoAsync(int contenedorPies, CancellationToken ct = default) =>
            Task.FromResult(Filas.Where(f => f.Contenedor == contenedorPies).Select(f => (int?)f.PesoMaximo).Max());
    }

    private WebApplicationFactory<clsProgram> Fabrica() =>
        fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRangosPesoContenedorPort>();
            s.AddSingleton<IRangosPesoContenedorPort>(new TablaDeMentira());
        }));

    private static HttpClient Cliente(WebApplicationFactory<clsProgram> fabrica, string rol)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    [Fact]
    public async Task Quienes_crean_solicitudes_y_quienes_costean_pueden_usarlo_y_el_resto_no()
    {
        using var fabrica = Fabrica();
        var cuerpo = new[] { new { indice = 0, claseContenedor = "CNT40.HC", pesoKg = 20_000 } };

        foreach (var rol in new[] { "Comercial", "AdminTarifas", "Admin" })
            Assert.Equal(HttpStatusCode.OK, (await Cliente(fabrica, rol).PostAsJsonAsync(Ruta, cuerpo)).StatusCode);

        foreach (var rol in new[] { "ServiciosLogisticos", "LiderProceso", "AdminBiblioteca" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Cliente(fabrica, rol).PostAsJsonAsync(Ruta, cuerpo)).StatusCode);

        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(Ruta, cuerpo)).StatusCode);
    }

    [Fact]
    public async Task Responde_por_lote_en_el_mismo_orden_con_el_codigo_NEXO_y_el_tope()
    {
        using var fabrica = Fabrica();
        using var c = Cliente(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, new object[]
        {
            new { indice = 5, claseContenedor = "CNT40.HC", pesoKg = 31_000 },
            new { indice = 2, claseContenedor = "20", pesoKg = 10_000 },
            new { indice = 9, claseContenedor = "CNT40.RE", pesoKg = 50_000 },
            new { indice = 11, claseContenedor = "granel", pesoKg = 1_000 },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var lista = await resp.Json();
        Assert.Equal([5, 2, 9, 11], lista.EnumerateArray().Select(x => x.GetProperty("indice").GetInt32()));
        Assert.Equal("3S3", lista[0].GetProperty("tipoVehiculo").GetString());
        Assert.Equal(40, lista[0].GetProperty("contenedorPies").GetInt32());
        Assert.True(lista[0].GetProperty("tablaDisponible").GetBoolean());
        Assert.Equal(34_000, lista[0].GetProperty("pesoMaximoContenedor").GetInt32());
        Assert.Equal("2S2", lista[1].GetProperty("tipoVehiculo").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, lista[2].GetProperty("tipoVehiculo").ValueKind);
        Assert.Equal(34_000, lista[2].GetProperty("pesoMaximoContenedor").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, lista[3].GetProperty("contenedorPies").ValueKind);
    }

    [Fact]
    public async Task Un_lote_vacio_o_con_peso_invalido_es_400()
    {
        using var fabrica = Fabrica();
        using var c = Cliente(fabrica, "Comercial");

        var vacio = await c.PostAsJsonAsync(Ruta, Array.Empty<object>());
        Assert.Equal(HttpStatusCode.BadRequest, vacio.StatusCode);
        Assert.Equal("SIN_LINEAS", await vacio.ErrorCode());

        var sinPeso = await c.PostAsJsonAsync(Ruta, new[] { new { indice = 0, claseContenedor = "CNT40.HC", pesoKg = 0 } });
        Assert.Equal(HttpStatusCode.BadRequest, sinPeso.StatusCode);
    }
}
