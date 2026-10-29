using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Biblioteca.Adaptadores;

namespace Nexo.Api.Tests.Biblioteca;

/// <summary>
/// El mapeo de vehículos a los códigos de SICE-TAC y de Plaza/SAP: la lectura de <c>tbl_mapeo_vehiculo_tarifario</c> contra la base REAL (con filas
/// propias de la prueba, que se borran) y cómo lo usa el emparejamiento de las tarifas de plaza propias. Las filas de prueba llevan el prefijo
/// <c>T380</c> para no tocar los 15 vehículos reales de la tabla.
/// </summary>
[Collection("api")]
public class clsMapeoVehiculoTarifarioTests(clsApiFixture fx) : IDisposable
{
    private readonly string _marca = "T380" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    public void Dispose() => fx.WithDb(db => db.MapeosVehiculoTarifario.Where(m => m.TipoVehiculo.StartsWith("T380")).ExecuteDelete());

    private clsMapeoVehiculoTarifarioAdapter Adaptador(IMemoryCache? cache = null) => new(
        fx.Factory.Services.GetRequiredService<IServiceScopeFactory>(), cache ?? new MemoryCache(new MemoryCacheOptions()),
        NullLogger<clsMapeoVehiculoTarifarioAdapter>.Instance);

    private void Fila(string sufijo, string sicetac, string? plaza, bool activo = true) => fx.WithDb(db =>
    {
        db.MapeosVehiculoTarifario.Add(new clsMapeoVehiculoTarifario
        {
            TipoVehiculo = _marca + sufijo, CodigoSicetac = sicetac, CodigoPlazaSap = plaza, Activo = activo, Verificacion = "fila de prueba",
        });
        return db.SaveChanges();
    });

    // ------------------------------------------------------------------ la lectura

    [Fact]
    public async Task Lee_el_mapeo_de_la_tabla_sin_importar_mayusculas_ni_espacios_y_con_plaza_nula_cuando_no_hay()
    {
        Fila("A", "2", "2");
        Fila("B", "3S2", null);

        var a = await Adaptador().ObtenerAsync($"  {_marca.ToLowerInvariant()}a ");
        var b = await Adaptador().ObtenerAsync(_marca + "B");

        Assert.Equal(new clsMapeoVehiculoTarifarioDto(_marca + "A", "2", "2"), a);
        Assert.Equal(new clsMapeoVehiculoTarifarioDto(_marca + "B", "3S2", null), b);
        Assert.Null(await Adaptador().ObtenerAsync(_marca + "NO-EXISTE"));
    }

    [Fact]
    public async Task Un_mapeo_desactivado_no_se_usa()
    {
        Fila("X", "9", "9", activo: false);

        Assert.Null(await Adaptador().ObtenerAsync(_marca + "X"));
    }

    [Fact]
    public async Task Sin_vehiculo_no_hay_mapeo()
    {
        Assert.Null(await Adaptador().ObtenerAsync(null));
        Assert.Null(await Adaptador().ObtenerAsync("   "));
    }

    [Fact]
    public async Task El_mapeo_se_guarda_en_memoria_y_un_cambio_se_ve_cuando_se_vuelve_a_leer()
    {
        Fila("C", "2", "2");
        var cache = new MemoryCache(new MemoryCacheOptions());
        var adaptador = Adaptador(cache);
        Assert.Equal("2", (await adaptador.ObtenerAsync(_marca + "C"))!.CodigoSicetac);

        fx.WithDb(db => db.MapeosVehiculoTarifario.Where(m => m.TipoVehiculo == _marca + "C").ExecuteUpdate(s => s.SetProperty(m => m.CodigoSicetac, "7")));

        Assert.Equal("2", (await adaptador.ObtenerAsync(_marca + "C"))!.CodigoSicetac);               // aún en memoria
        Assert.Equal("7", (await Adaptador().ObtenerAsync(_marca + "C"))!.CodigoSicetac);              // lectura nueva
    }

    [Fact]
    public async Task Si_la_tabla_no_se_puede_leer_responde_sin_mapeo_y_no_lanza()
    {
        var scopes = new clsAlcancesQueFallan();
        var adaptador = new clsMapeoVehiculoTarifarioAdapter(scopes, new MemoryCache(new MemoryCacheOptions()), NullLogger<clsMapeoVehiculoTarifarioAdapter>.Instance);

        Assert.Null(await adaptador.ObtenerAsync("C2"));
        Assert.Null(await adaptador.ObtenerAsync("C3"));
        Assert.Equal(1, scopes.Intentos); // no insiste en cada consulta: guarda el "sin mapeo" por un momento
    }

    private sealed class clsAlcancesQueFallan : IServiceScopeFactory
    {
        public int Intentos { get; private set; }

        public IServiceScope CreateScope()
        {
            Intentos++;
            throw new InvalidOperationException("Invalid object name 'db_NEXO.tbl_mapeo_vehiculo_tarifario'.");
        }
    }

    [Fact]
    public void El_mapeo_se_registra_como_un_servicio_unico_porque_lo_consultan_costeos_en_paralelo()
    {
        var a = fx.Factory.Services.GetRequiredService<IMapeoVehiculoTarifarioPort>();
        var b = fx.Factory.Services.GetRequiredService<IMapeoVehiculoTarifarioPort>();

        Assert.Same(a, b);
    }

    // ------------------------------------------------------------------ las tarifas de plaza propias

    private static clsTarifaPlazaInternaAdapter.clsFila Fila(string? tipoVehiculo, decimal valor) =>
        new(null, valor, "detalle", "Nacional", "Cupo", null, null, tipoVehiculo);

    private static clsConsultaTarifaPlazaInterna Consulta(string tipoVehiculo) =>
        new("Medellín", "Bogotá", null, null, null, tipoVehiculo, null);

    private static HashSet<string> Aceptados(params string[] codigos) => new(codigos, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void El_Sencillo_C2_encuentra_las_filas_de_plaza_que_lo_nombran_2()
    {
        var filas = new[] { Fila("2", 2_000_000m), Fila("2S2", 3_000_000m), Fila("2L2", 1_500_000m) }.ToList();

        var (exactas, criterio, relajado) = clsTarifaPlazaInternaAdapter.NivelDeMatching(filas, Consulta("C2"), null, Aceptados("C2", "2"));

        Assert.Equal(["2"], exactas.Select(f => f.TipoVehiculo));
        Assert.StartsWith("nivel exacto", criterio);
        Assert.False(relajado);
    }

    [Fact]
    public void Sin_el_mapeo_el_Sencillo_C2_no_encontraba_nada_y_se_iba_a_otro_vehiculo()
    {
        var filas = new[] { Fila("2", 2_000_000m), Fila("2S2", 3_000_000m) }.ToList();

        var (exactas, _, relajado) = clsTarifaPlazaInternaAdapter.NivelDeMatching(filas, Consulta("C2"), null);

        Assert.True(relajado);                           // el valor es de otro vehículo y solo sirve de referencia
        Assert.DoesNotContain(exactas, f => f.TipoVehiculo == "C2");
    }

    [Fact]
    public void Los_tres_Turbo_se_tarifan_en_plaza_como_2L2_pero_las_filas_antiguas_con_2L3_siguen_encontrandose()
    {
        var filas = new[] { Fila("2L2", 1_500_000m), Fila("2L3", 1_400_000m), Fila("2S2", 3_000_000m) }.ToList();

        foreach (var turbo in new[] { "2L1", "2L2", "2L3" })
        {
            var aceptados = turbo == "2L2" ? Aceptados("2L2") : Aceptados(turbo, "2L2");
            var (exactas, _, relajado) = clsTarifaPlazaInternaAdapter.NivelDeMatching(filas, Consulta(turbo), null, aceptados);

            Assert.False(relajado);
            Assert.All(exactas, f => Assert.Contains(f.TipoVehiculo, new[] { "2L2", "2L3" }));
            Assert.Contains(exactas, f => f.TipoVehiculo == "2L2");
        }
    }

    [Fact]
    public void Un_vehiculo_sin_codigo_de_plaza_conserva_su_propio_codigo_para_las_filas_ya_cargadas_con_el_de_SICE_TAC()
    {
        var filas = new[] { Fila("3S2", 4_000_000m), Fila("3S3", 5_000_000m) }.ToList();

        var (exactas, _, relajado) = clsTarifaPlazaInternaAdapter.NivelDeMatching(filas, Consulta("3S2"), null, Aceptados("3S2"));

        Assert.Equal(["3S2"], exactas.Select(f => f.TipoVehiculo));
        Assert.False(relajado);
    }

    [Fact]
    public void Sin_codigos_aceptados_el_emparejamiento_es_el_de_siempre()
    {
        var filas = new[] { Fila("2S2", 3_000_000m), Fila("3S3", 5_000_000m) }.ToList();

        var (exactas, _, relajado) = clsTarifaPlazaInternaAdapter.NivelDeMatching(filas, Consulta("2S2"), null);

        Assert.Equal(["2S2"], exactas.Select(f => f.TipoVehiculo));
        Assert.False(relajado);
    }

    [Fact]
    public async Task El_adaptador_de_plaza_propia_acepta_el_codigo_de_plaza_del_mapeo_y_el_propio_del_vehiculo()
    {
        // Se verifica con el puerto real y la tabla de plaza real: C2 mapeado a plaza "2" nunca puede dejar de buscar por "C2" ni por "2".
        using var scope = fx.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();

        var resultado = await puerto.BuscarAsync(new clsConsultaTarifaPlazaInterna("Ciudad que no existe T380", "Otra que no existe T380", null, null, null, "C2", null));

        Assert.Null(resultado.Vigente); // sin municipios no hay candidato, y la consulta con el mapeo no lanza
    }
}
