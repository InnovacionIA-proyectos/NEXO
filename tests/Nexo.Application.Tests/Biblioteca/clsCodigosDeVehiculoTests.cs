using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

namespace Nexo.Application.Tests.Biblioteca;

/// <summary>
/// Con qué código se busca cada vehículo de NEXO en SICE-TAC y en Plaza/SAP. Las filas del mapeo se leen del mismo CSV con el que se carga la
/// tabla (<c>docs/Database/seeder/mapeo_vehiculos.csv</c>), así que la prueba cubre los 15 vehículos reales y avisa si el archivo cambia.
/// </summary>
public class clsCodigosDeVehiculoTests
{
    private sealed class clsMapeoDeCsv : IMapeoVehiculoTarifarioPort
    {
        private readonly Dictionary<string, clsMapeoVehiculoTarifarioDto> _filas;

        public clsMapeoDeCsv() => _filas = Leer().ToDictionary(f => f.TipoVehiculo.ToUpperInvariant());

        public int Consultas { get; private set; }

        public Task<clsMapeoVehiculoTarifarioDto?> ObtenerAsync(string? tipoVehiculo, CancellationToken ct = default)
        {
            Consultas++;
            return Task.FromResult(string.IsNullOrWhiteSpace(tipoVehiculo) ? null : _filas.GetValueOrDefault(tipoVehiculo.Trim().ToUpperInvariant()));
        }
    }

    private sealed class clsSinMapeo : IMapeoVehiculoTarifarioPort
    {
        public Task<clsMapeoVehiculoTarifarioDto?> ObtenerAsync(string? tipoVehiculo, CancellationToken ct = default) =>
            Task.FromResult<clsMapeoVehiculoTarifarioDto?>(null);
    }

    public static IReadOnlyList<clsMapeoVehiculoTarifarioDto> Leer()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Nexo.Clean.sln"))) dir = dir.Parent;
        var ruta = Path.Combine(dir!.FullName, "docs", "Database", "seeder", "mapeo_vehiculos.csv");

        return File.ReadAllLines(ruta).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l =>
        {
            var c = l.Split(',');
            return new clsMapeoVehiculoTarifarioDto(c[0], c[1], string.IsNullOrWhiteSpace(c[2]) ? null : c[2]);
        }).ToList();
    }

    public static IEnumerable<object[]> TodosLosVehiculos() => Leer().Select(f => new object[] { f.TipoVehiculo });

    // ------------------------------------------------------------------ los casos que motivaron el mapeo

    [Fact]
    public async Task El_Sencillo_C2_se_busca_como_2_en_SICE_TAC_y_en_Plaza_SAP()
    {
        var mapeo = new clsMapeoDeCsv();

        Assert.Equal("2", await mapeo.SicetacAsync("C2"));
        var plaza = await mapeo.PlazaAsync("C2");
        Assert.Equal(("2", false), (plaza.Codigo, plaza.SinTarifaDePlaza));
    }

    [Theory]
    [InlineData("2L1", "2L1", "2L2")]
    [InlineData("2L2", "2L2", "2L2")]
    [InlineData("2L3", "2L3", "2L2")]
    public async Task El_Turbo_conserva_su_codigo_en_SICE_TAC_y_en_plaza_se_tarifa_como_2L2(string tipo, string sicetac, string plazaEsperada)
    {
        var mapeo = new clsMapeoDeCsv();

        Assert.Equal(sicetac, await mapeo.SicetacAsync(tipo));
        Assert.Equal(plazaEsperada, (await mapeo.PlazaAsync(tipo)).Codigo);
    }

    [Fact]
    public async Task El_Dobletroque_C3_es_3_en_SICE_TAC_y_C3_en_plaza()
    {
        var mapeo = new clsMapeoDeCsv();

        Assert.Equal("3", await mapeo.SicetacAsync("C3"));
        Assert.Equal("C3", (await mapeo.PlazaAsync("C3")).Codigo);
    }

    [Theory]
    [InlineData("2_5")]
    [InlineData("2_7")]
    [InlineData("3S2")]
    [InlineData("CA_")]
    [InlineData("V2")]
    [InlineData("V3")]
    [InlineData("V4")]
    public async Task Los_vehiculos_que_solo_existen_en_SICE_TAC_no_tienen_tarifa_de_plaza_y_CA_no_pierde_la_C(string tipo)
    {
        var mapeo = new clsMapeoDeCsv();

        Assert.Equal(tipo, await mapeo.SicetacAsync(tipo));
        var plaza = await mapeo.PlazaAsync(tipo);
        Assert.Null(plaza.Codigo);
        Assert.True(plaza.SinTarifaDePlaza);
    }

    [Theory]
    [MemberData(nameof(TodosLosVehiculos))]
    public async Task Cada_vehiculo_del_mapeo_resuelve_exactamente_a_los_codigos_de_la_carga_inicial(string tipo)
    {
        var mapeo = new clsMapeoDeCsv();
        var esperado = Leer().Single(f => f.TipoVehiculo == tipo);

        Assert.Equal(esperado.CodigoSicetac, await mapeo.SicetacAsync(tipo));
        var plaza = await mapeo.PlazaAsync(tipo);
        Assert.Equal(esperado.CodigoPlazaSap, plaza.Codigo);
        Assert.Equal(esperado.CodigoPlazaSap is null, plaza.SinTarifaDePlaza);
    }

    [Fact]
    public void La_carga_inicial_trae_los_15_vehiculos_sin_repetir()
    {
        var filas = Leer();

        Assert.Equal(15, filas.Count);
        Assert.Equal(15, filas.Select(f => f.TipoVehiculo.ToUpperInvariant()).Distinct().Count());
    }

    // ------------------------------------------------------------------ lo que no está mapeado se comporta como antes

    [Theory]
    [InlineData("  c2  ")]
    [InlineData("c2")]
    public async Task El_vehiculo_se_busca_sin_importar_mayusculas_ni_espacios(string tipo)
    {
        Assert.Equal("2", await new clsMapeoDeCsv().SicetacAsync(tipo));
    }

    [Fact]
    public async Task Un_vehiculo_sin_fila_de_mapeo_se_busca_con_su_propio_codigo_normalizado()
    {
        var mapeo = new clsSinMapeo();

        Assert.Equal("XYZ", await mapeo.SicetacAsync(" xyz "));
        var plaza = await mapeo.PlazaAsync(" xyz ");
        Assert.Equal(("XYZ", false), (plaza.Codigo, plaza.SinTarifaDePlaza));
    }

    [Fact]
    public async Task Sin_fila_de_mapeo_se_puede_conservar_la_regla_anterior_de_quitar_la_C_inicial()
    {
        var mapeo = new clsSinMapeo();

        Assert.Equal("4", await mapeo.SicetacAsync("C4", v => v.Trim().ToUpperInvariant().TrimStart('C')));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_vehiculo_no_hay_codigo_ni_se_consulta_el_mapeo(string? vacio)
    {
        var mapeo = new clsMapeoDeCsv();

        Assert.Null(await mapeo.SicetacAsync(vacio));
        var plaza = await mapeo.PlazaAsync(vacio);
        Assert.Equal((null, false), (plaza.Codigo, plaza.SinTarifaDePlaza));
        Assert.Equal(0, mapeo.Consultas);
    }
}
