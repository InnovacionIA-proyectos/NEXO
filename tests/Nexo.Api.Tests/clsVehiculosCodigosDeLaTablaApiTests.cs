using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Domain.Catalogos.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// Los códigos de <c>tbl_mapeo_vehiculo_tarifario</c> contra la API y la base REALES: el listado de códigos de NEXO para la Mesa (con <c>enSap</c>)
/// y que un tipo que es uno de esos códigos nazca oficial al resolver el texto libre, salvo con una carrocería fuera del catálogo. Todo lleva una marca única
/// y se borra al terminar; el catálogo de SAP es de mentira.
/// </summary>
[Collection("api")]
public class clsVehiculosCodigosDeLaTablaApiTests(clsApiFixture fx)
{
    private static string Marca() => $"T446{Guid.NewGuid():N}"[..9].ToUpperInvariant();

    private sealed class SapDeMentira(params string[] codigos) : ITiposVehiculoPort
    {
        public Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<clsTipoVehiculoDto>>(codigos.Select(c => new clsTipoVehiculoDto(c, c, true)).ToList());
    }

    private WebApplicationFactory<clsProgram> Fabrica(params string[] codigosSap) =>
        fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITiposVehiculoPort>();
            s.AddSingleton<ITiposVehiculoPort>(new SapDeMentira(codigosSap));
        }));

    private static HttpClient Cliente(WebApplicationFactory<clsProgram> fabrica, string rol)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    private void Mapeo(string codigo, string? nombre = null) => fx.WithDb(db =>
    {
        db.MapeosVehiculoTarifario.Add(new clsMapeoVehiculoTarifario { TipoVehiculo = codigo, CodigoSicetac = codigo, NombreSicetac = nombre });
        return db.SaveChanges();
    });

    private void Limpiar(string marca) => fx.WithDb(db =>
    {
        db.ConfiguracionesVehiculo.Where(v => v.TipoVehiculo.StartsWith(marca)).ExecuteDelete();
        db.MapeosVehiculoTarifario.Where(m => m.TipoVehiculo.StartsWith(marca)).ExecuteDelete();
        db.Rutas.Where(r => r.Origen.StartsWith(marca)).ExecuteDelete();
        return 0;
    });

    [Fact]
    public async Task El_listado_trae_los_codigos_de_NEXO_con_su_nombre_y_si_estan_en_SAP_y_lo_ve_cualquier_rol()
    {
        var marca = Marca();
        Mapeo(marca + "A", "Vehículo de prueba A");
        Mapeo(marca + "B");
        using var fabrica = Fabrica(marca + "A");
        try
        {
            foreach (var rol in new[] { "Comercial", "AdminTarifas", "ServiciosLogisticos", "LiderProceso", "AdminBiblioteca", "Admin" })
                using (var c = Cliente(fabrica, rol))
                    Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/catalogos/vehiculos-nexo")).StatusCode);

            using var admin = Cliente(fabrica, "Admin");
            var lista = (await (await admin.GetAsync("/api/catalogos/vehiculos-nexo")).Json()).EnumerateArray()
                .Where(x => x.GetProperty("codigo").GetString()!.StartsWith(marca)).ToList();

            Assert.Equal([marca + "A", marca + "B"], lista.Select(x => x.GetProperty("codigo").GetString()));
            Assert.Equal("Vehículo de prueba A", lista[0].GetProperty("descripcion").GetString());
            Assert.True(lista[0].GetProperty("enSap").GetBoolean());
            Assert.Equal(marca + "B", lista[1].GetProperty("descripcion").GetString());
            Assert.False(lista[1].GetProperty("enSap").GetBoolean());
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Un_codigo_de_la_tabla_nace_oficial_y_con_una_carroceria_fuera_del_catalogo_nace_en_cuarentena()
    {
        var marca = Marca();
        Mapeo(marca + "V");
        using var fabrica = Fabrica();
        using var c = Cliente(fabrica, "Comercial");
        try
        {
            var resp = await c.PostAsJsonAsync("/api/solicitudes/resolver-catalogos", new object[]
            {
                new { origen = marca + " A", destino = marca + " B", tipoVehiculo = marca + "V" },
                new { origen = marca + " A", destino = marca + " C", tipoVehiculo = marca + "V", carroceria = "Carrocería inventada " + marca },
                new { origen = marca + " A", destino = marca + " D", tipoVehiculo = marca + " libre" },
            });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var ids = (await resp.Json()).EnumerateArray().Select(x => x.GetProperty("configVehiculoId").GetGuid()).ToList();
            var oficial = fx.WithDb(db => ids.Select(id => db.ConfiguracionesVehiculo.Single(v => v.Id == id).EsCatalogoOficial).ToList());
            Assert.Equal([true, false, false], oficial);
        }
        finally { Limpiar(marca); }
    }
}
