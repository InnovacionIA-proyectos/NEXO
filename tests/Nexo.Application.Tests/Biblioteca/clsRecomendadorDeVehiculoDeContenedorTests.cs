using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

namespace Nexo.Application.Tests.Biblioteca;

/// <summary>
/// El vehículo que lleva un contenedor de 20 o 40 pies según su peso. Los rangos se leen del mismo CSV con el que se carga
/// <c>tbl_rangos_peso_contenedor</c> (<c>docs/Database/seeder/rangos_peso_contenedor.csv</c>), así que la prueba cubre los 10 rangos reales y avisa si
/// el archivo cambia. Incluye los pesos cuyo vehículo cambia respecto de los rangos de SAP que se usaban antes.
/// </summary>
public class clsRecomendadorDeVehiculoDeContenedorTests
{
    private static IReadOnlyList<clsRangoPesoContenedorDto> Rangos()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Nexo.Clean.sln"))) dir = dir.Parent;
        var ruta = Path.Combine(dir!.FullName, "docs", "Database", "seeder", "rangos_peso_contenedor.csv");

        return File.ReadAllLines(ruta).Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l =>
        {
            var c = l.Split(',');
            return new clsRangoPesoContenedorDto(int.Parse(c[0]), c[1], int.Parse(c[2]));
        }).ToList();
    }

    private static string? Elegir(int pies, decimal peso) => clsRecomendadorDeVehiculoDeContenedor.Elegir(Rangos(), pies, peso);

    [Fact]
    public void La_carga_inicial_trae_los_10_rangos_cinco_por_tamano_con_topes_que_suben_sin_repetirse()
    {
        var rangos = Rangos();

        Assert.Equal(10, rangos.Count);
        foreach (var pies in new[] { 20, 40 })
        {
            var topes = rangos.Where(r => r.Contenedor == pies).Select(r => r.PesoMaximo).ToList();
            Assert.Equal(5, topes.Count);
            Assert.Equal(topes.Count, topes.Distinct().Count());
        }
    }

    // ------------------------------------------------------------------ cada rango, en sus bordes

    [Theory]
    [InlineData(40, 1, "2L3")]
    [InlineData(40, 8_000, "2L3")]
    [InlineData(40, 8_001, "2S2")]
    [InlineData(40, 18_000, "2S2")]
    [InlineData(40, 18_001, "2S3")]
    [InlineData(40, 23_000, "2S3")]
    [InlineData(40, 23_001, "3S2")]
    [InlineData(40, 30_000, "3S2")]
    [InlineData(40, 30_001, "3S3")]
    [InlineData(40, 34_000, "3S3")]
    [InlineData(20, 1, "C2")]
    [InlineData(20, 8_000, "C2")]
    [InlineData(20, 8_001, "2S2")]
    [InlineData(20, 14_000, "2S2")]
    [InlineData(20, 14_001, "2S3")]
    [InlineData(20, 18_000, "2S3")]
    [InlineData(20, 18_001, "3S2")]
    [InlineData(20, 25_000, "3S2")]
    [InlineData(20, 25_001, "3S3")]
    [InlineData(20, 30_000, "3S3")]
    public void Se_escoge_la_fila_de_menor_peso_maximo_que_alcance_para_el_peso(int pies, decimal peso, string esperado)
    {
        Assert.Equal(esperado, Elegir(pies, peso));
    }

    [Theory]
    [InlineData(40, 34_001)]
    [InlineData(40, 60_000)]
    [InlineData(20, 30_001)]
    [InlineData(20, 40_000)]
    public void Por_encima_del_tope_mas_alto_ningun_vehiculo_aplica(int pies, decimal peso)
    {
        Assert.Null(Elegir(pies, peso));
    }

    [Fact]
    public void Un_tamano_que_no_esta_en_la_tabla_no_tiene_vehiculo()
    {
        Assert.Null(Elegir(45, 10_000));
    }

    // ------------------------------------------------------------------ los pesos que cambian de vehículo respecto de los rangos de SAP

    [Theory]
    [InlineData(40, 25_000, "3S2")]   // con SAP era 3S3
    [InlineData(40, 22_500, "2S3")]   // con SAP era 3S3
    [InlineData(40, 5_000, "2L3")]    // con SAP era 2S2
    [InlineData(20, 8_200, "2S2")]    // con SAP era el camión de dos ejes
    public void Los_pesos_que_cambian_de_vehiculo_frente_a_los_rangos_de_SAP_dan_el_de_la_tabla_nueva(int pies, decimal peso, string esperado)
    {
        Assert.Equal(esperado, Elegir(pies, peso));
    }

    [Fact]
    public void Un_peso_mayor_nunca_da_un_vehiculo_de_tope_menor()
    {
        foreach (var pies in new[] { 20, 40 })
        {
            var rangos = Rangos();
            var topeAnterior = 0;
            for (var peso = 1; peso <= 35_000; peso += 250)
            {
                var vehiculo = clsRecomendadorDeVehiculoDeContenedor.Elegir(rangos, pies, peso);
                if (vehiculo is null) continue;
                var tope = rangos.Single(r => r.Contenedor == pies && r.TipoVehiculo == vehiculo).PesoMaximo;
                Assert.True(tope >= topeAnterior, $"{pies} pies, {peso} kg: {vehiculo}");
                Assert.True(tope >= peso);
                topeAnterior = tope;
            }
        }
    }

    // ------------------------------------------------------------------ el tamaño sale de la clase de contenedor

    [Theory]
    [InlineData("CNT40.HC", 40)]
    [InlineData("CNT20.IT", 20)]
    [InlineData("CNT20.ST", 20)]
    [InlineData("cnt40.hc", 40)]
    [InlineData("40 HC", 40)]
    [InlineData("Contenedor de 20 pies", 20)]
    [InlineData("CNT45.HC", null)]
    [InlineData("CNT400.XX", null)]
    [InlineData("General", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void El_tamano_en_pies_sale_de_la_clase_de_contenedor(string? clase, int? esperado)
    {
        Assert.Equal(esperado, clsRecomendadorDeVehiculoDeContenedor.Pies(clase));
    }
}
