using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CabinaFletes.Domain.Tests;

/// <summary>
/// Integridad del dataset de tarifas de plaza provisionales de la HU S2-HU-09.2
/// (issue #32) — <c>docs/tarifas-plaza-provisionales/dataset-tarifas-plaza-provisionales.json</c>.
///
/// No es una prueba de <see cref="Costeo.Reglas.ReglaDeCosteo"/>: esa regla no tiene
/// hoy ninguna noción de "fuente provisional" a la que aplicarse (eso depende de la
/// issue #33, no construida). Esto solo confirma que el archivo tiene la forma que
/// documenta el README y que los conteos no se desactualizaron en silencio — el
/// archivo se lee DIRECTO de <c>docs/</c>, sin copia duplicada, para que una
/// regeneración del dataset y este chequeo nunca puedan divergir.
/// </summary>
public class TarifasPlazaProvisionalesDatasetTests
{
    private static readonly JsonElement Dataset = CargarDataset();

    private static JsonElement CargarDataset()
    {
        var ruta = RutaDataset();
        var json = File.ReadAllText(ruta);
        return JsonDocument.Parse(json).RootElement;
    }

    private static string RutaDataset([CallerFilePath] string aqui = "")
    {
        var carpetaDeEsteArchivo = Path.GetDirectoryName(aqui)!;
        var ruta = Path.Combine(
            carpetaDeEsteArchivo, "..", "..", "docs", "tarifas-plaza-provisionales",
            "dataset-tarifas-plaza-provisionales.json");
        return Path.GetFullPath(ruta);
    }

    private static JsonElement CasosBorde(string categoria) =>
        Dataset.GetProperty("casosBorde").GetProperty(categoria);

    [Fact]
    public void El_archivo_del_dataset_existe_donde_dice_el_README()
    {
        Assert.True(File.Exists(RutaDataset()), $"No se encontró {RutaDataset()}");
    }

    [Fact]
    public void Los_conteos_declarados_coinciden_con_las_filas_reales_del_archivo()
    {
        var nucleo = Dataset.GetProperty("nucleo");
        Assert.Equal(150, nucleo.GetArrayLength());
        Assert.Equal(150, Dataset.GetProperty("totalNucleo").GetInt32());

        var totalCasosBorde =
            CasosBorde("vigenciaVencida").GetArrayLength() +
            CasosBorde("vigenciaAbsurda").GetArrayLength() +
            CasosBorde("valorVacio").GetArrayLength() +
            CasosBorde("valorMaximo").GetArrayLength() +
            CasosBorde("valorMinimo").GetArrayLength() +
            CasosBorde("sinPlaza").GetArrayLength();

        Assert.Equal(60, totalCasosBorde);
        Assert.Equal(60, Dataset.GetProperty("totalCasosBorde").GetInt32());
    }

    [Theory]
    [InlineData("vigenciaVencida", 30)]
    [InlineData("vigenciaAbsurda", 2)]
    [InlineData("valorVacio", 3)]
    [InlineData("valorMaximo", 5)]
    [InlineData("valorMinimo", 5)]
    [InlineData("sinPlaza", 15)]
    public void Cada_categoria_de_caso_borde_pedida_por_la_HU_tiene_las_filas_documentadas(
        string categoria, int filasEsperadas)
    {
        // sin plaza, vigencia vencida, valores extremos — los 3 casos borde que
        // pide explícitamente el criterio de cierre, más el desglose real que se
        // encontró al extraer (absurda/vacío/máximo/mínimo son subcategorías reales
        // de "valores extremos").
        Assert.Equal(filasEsperadas, CasosBorde(categoria).GetArrayLength());
    }

    [Fact]
    public void El_nucleo_es_representativo_no_una_sola_ruta_repetida()
    {
        var rutas = new HashSet<string>();
        var vehiculos = new HashSet<string>();
        foreach (var l in Dataset.GetProperty("nucleo").EnumerateArray())
        {
            rutas.Add($"{l.GetProperty("origen").GetString()}|{l.GetProperty("destino").GetString()}");
            var vehiculo = l.GetProperty("vehiculoDescripcion");
            vehiculos.Add(vehiculo.ValueKind == JsonValueKind.Null ? "(sin vehiculo)" : vehiculo.GetString()!);
        }

        Assert.True(rutas.Count >= 30, $"Solo {rutas.Count} rutas distintas en el núcleo — poca diversidad.");
        Assert.True(vehiculos.Count >= 3, $"Solo {vehiculos.Count} tipos de vehículo distintos en el núcleo.");
    }

    [Fact]
    public void Las_filas_de_valor_vacio_realmente_no_tienen_valor()
    {
        foreach (var l in CasosBorde("valorVacio").EnumerateArray())
            Assert.Equal(JsonValueKind.Null, l.GetProperty("valor").ValueKind);
    }

    [Fact]
    public void Las_filas_de_vigencia_absurda_tienen_una_fecha_mas_alla_de_2030()
    {
        foreach (var l in CasosBorde("vigenciaAbsurda").EnumerateArray())
        {
            var vigencia = l.GetProperty("vigencia").GetString()!;
            var anio = int.Parse(vigencia[..4]);
            Assert.True(anio > 2030, $"Vigencia {vigencia} no es absurda.");
        }
    }

    [Fact]
    public void Las_filas_sin_plaza_no_traen_un_valor_de_tarifa_solo_frecuencia_historica()
    {
        // "sin plaza" es ausencia real de fila en tblSAPTarifas, no una fila con
        // valor nulo — por eso su forma en el JSON ni siquiera tiene campo "valor".
        foreach (var l in CasosBorde("sinPlaza").EnumerateArray())
        {
            Assert.False(l.TryGetProperty("valor", out _));
            Assert.True(l.GetProperty("frecuenciaHistorica").GetInt32() > 0);
        }
    }

    [Fact]
    public void Los_valores_maximo_y_minimo_documentados_son_consistentes_entre_si()
    {
        var maximos = CasosBorde("valorMaximo").EnumerateArray().Select(l => l.GetProperty("valor").GetDecimal());
        var minimos = CasosBorde("valorMinimo").EnumerateArray().Select(l => l.GetProperty("valor").GetDecimal());

        Assert.True(maximos.Min() > minimos.Max(),
            "El menor de los 'valores máximos' debería seguir siendo mayor que el mayor de los 'valores mínimos'.");
    }
}
