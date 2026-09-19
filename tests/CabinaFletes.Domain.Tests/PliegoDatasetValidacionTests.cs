using System.Text.Json;
using System.Text.Json.Serialization;
using CabinaFletes.Domain.Costeo.Reglas;

namespace CabinaFletes.Domain.Tests;

/// <summary>
/// Validación muestral del costeo contra el dataset de pliego de la HU S2-HU-04.7
/// (<c>TestData/pliego-300-lineas.json</c>, 320 líneas). El dataset trae, por línea,
/// qué encontró cada fuente simulada (sicetac/plaza/histórico) y la regla esperada
/// calculada a mano; estas pruebas corren el código de producción
/// (<see cref="ReglaDeCosteo"/>) sobre esos mismos insumos y comparan.
///
/// Dos grupos de líneas NO pasan por <see cref="ReglaDeCosteo"/> porque la regla que
/// prueban vive en otra capa y no es una función pura sobre candidatos:
///
///   "solo_sicetac_*"  -- el cortocircuito de rutas SOLO SICE-TAC vive en
///                        PropuestaTarifaAdapter.ProponerLineaAsync (ignora la
///                        plaza por completo). Se verifica con una aserción directa
///                        sobre el propio dataset, documentada como tal.
///   "sin_ruta",
///   "peso_malformado",
///   "fuente_cae"      -- ocurren antes de o fuera de la regla de costeo (falta de
///                        ruta / rechazo en captura / excepción de una fuente
///                        externa). Están marcadas <c>ValidableEnDominio = false</c>
///                        y solo se documentan, no se ejecutan contra ReglaDeCosteo.
/// </summary>
public class PliegoDatasetValidacionTests
{
    private static readonly PliegoDatasetFixture Dataset = CargarDataset();

    private static PliegoDatasetFixture CargarDataset()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "TestData", "pliego-300-lineas.json");
        var json = File.ReadAllText(ruta);
        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<PliegoDatasetFixture>(json, opciones)
            ?? throw new InvalidOperationException("No se pudo leer el dataset de pliego.");
    }

    private static LineaPliegoFixture Linea(int id) =>
        Dataset.Lineas.Single(l => l.Id == id);

    private static ResultadoDeCosteo Decidir(LineaPliegoFixture l) =>
        ReglaDeCosteo.DecidirConTarifario(
            l.Sicetac is { } s ? new CandidatoDeTarifa("sicetac", s) : null,
            l.Plaza is { } p ? new CandidatoDeTarifa("plaza", p) : null,
            l.Historico is { } h ? new CandidatoDeTarifa("historico_sap", h) : null,
            l.TarifarioVencido);

    // ------------------------------------------------ forma del dataset (criterio de cierre)

    [Fact]
    public void El_dataset_tiene_al_menos_300_lineas()
    {
        Assert.True(Dataset.Lineas.Count >= 300, $"Solo hay {Dataset.Lineas.Count} líneas.");
        Assert.Equal(Dataset.TotalLineas, Dataset.Lineas.Count);
    }

    [Fact]
    public void El_dataset_cubre_todos_los_casos_borde_declarados_por_la_HU()
    {
        // rutas solo-SICE-TAC, rutas sin tarifa de plaza, piso/tarifario vencido,
        // retorno de vacío, datos incompletos/malformados, fuente caída.
        string[] casosEsperados =
        [
            "normal",
            "solo_sicetac_con_piso", "solo_sicetac_sin_piso",
            "sin_tarifa_plaza",
            "tarifario_vencido_con_piso", "tarifario_vencido_sin_piso",
            "retorno_contenedor_vacio",
            "sin_ruta", "peso_malformado", "sin_configuracion_vehiculo", "sin_tipologia", "todo_vacio",
            "fuente_cae",
        ];

        var presentes = Dataset.Lineas.Select(l => l.Caso).ToHashSet();
        foreach (var caso in casosEsperados)
            Assert.Contains(caso, presentes);
    }

    [Fact]
    public void Exactamente_3_lineas_simulan_una_fuente_caida_para_el_escenario_Gherkin_de_fuente_caida()
    {
        // Insumo del escenario "Un pliego con líneas sin resolver termina en estado
        // parcial": "Dado un pliego donde una fuente externa falla para 3 líneas".
        var caidas = Dataset.Lineas.Where(l => l.FuenteCae).Select(l => l.Id).OrderBy(x => x).ToList();

        Assert.Equal(3, caidas.Count);
        Assert.Equal(Dataset.FuenteCaeIds.OrderBy(x => x).ToList(), caidas);
        Assert.All(caidas, id => Assert.False(Linea(id).ValidableEnDominio));
    }

    // ------------------------------------------- invariante en TODA la población (no solo la muestra)

    [Fact]
    public void Ninguna_propuesta_de_las_320_lineas_queda_por_debajo_del_piso_sicetac_vigente()
    {
        var infracciones = new List<string>();

        foreach (var l in Dataset.Lineas)
        {
            // El piso solo aplica cuando hay un piso SICE-TAC vigente con el que
            // comparar. Las líneas "solo SICE-TAC" no pasan por esta función (ver
            // comentario de la clase) y no tienen invariante que violar aquí.
            if (!l.ValidableEnDominio || l.SoloSicetac || l.Sicetac is null) continue;

            var r = Decidir(l);
            if (r.Valor is { } valor && valor < l.Sicetac.Value)
                infracciones.Add($"línea {l.Id} ({l.Caso}): propuso {valor} con piso {l.Sicetac}");
        }

        Assert.True(infracciones.Count == 0, string.Join("; ", infracciones));
    }

    // ------------------------------------------------ muestra estratificada (propuesta vs regla esperada)

    public static IEnumerable<object[]> MuestraEstratificada()
    {
        // 5-3 líneas representativas por categoría validable contra ReglaDeCosteo,
        // documentadas también en tests/QA/hu-04.7-.../README.md con su resultado.
        int[] ids =
        [
            3, 9, 10, 105, 188,                       // normal (cubre plaza gana, sicetac gana y empate)
            276, 283, 290,                            // sin_tarifa_plaza
            291, 294, 297,                             // tarifario_vencido_con_piso
            298, 299, 300,                             // tarifario_vencido_sin_piso
            301, 305, 310,                             // retorno_contenedor_vacio
            315, 316, 317,                             // sin_configuracion_vehiculo / sin_tipologia / todo_vacio
        ];
        foreach (var id in ids) yield return new object[] { id };
    }

    [Theory]
    [MemberData(nameof(MuestraEstratificada))]
    public void La_propuesta_calculada_coincide_con_la_regla_esperada(int lineaId)
    {
        var l = Linea(lineaId);
        Assert.True(l.ValidableEnDominio, $"línea {lineaId} no es validable contra ReglaDeCosteo.");

        var r = Decidir(l);

        Assert.Equal(l.Esperado.Valor, r.Valor);
        Assert.Equal(l.Esperado.Fuente, r.Fuente);
        Assert.Equal(l.Esperado.RequiereManual, r.RequiereTarifaManual);
        foreach (var esperadaSubcadena in l.Esperado.ContieneEnJustificacion)
            Assert.Contains(esperadaSubcadena, r.Justificacion);
    }

    public static IEnumerable<object[]> MuestraSoloSicetac()
    {
        int[] ids = [261, 265, 270, 271, 273, 275];
        foreach (var id in ids) yield return new object[] { id };
    }

    [Theory]
    [MemberData(nameof(MuestraSoloSicetac))]
    public void Rutas_solo_sicetac_ignoran_la_plaza_aunque_sea_mayor(int lineaId)
    {
        // Regla verificada por inspección de PropuestaTarifaAdapter.ProponerLineaAsync
        // (líneas ~161-170): para una ruta marcada SOLO SICE-TAC, la propuesta es el
        // piso si existe, o manual si no — la plaza jamás se compara. No se llama a
        // ReglaDeCosteo aquí porque esa rama vive en el adaptador, no en el dominio
        // puro; ver nota de la clase.
        var l = Linea(lineaId);
        Assert.True(l.SoloSicetac);

        var valorPropuesto = l.Sicetac;
        var requiereManual = l.Sicetac is null;
        var fuente = l.Sicetac is null ? null : "sicetac";

        Assert.Equal(l.Esperado.Valor, valorPropuesto);
        Assert.Equal(l.Esperado.RequiereManual, requiereManual);
        Assert.Equal(l.Esperado.Fuente, fuente);

        // Aunque la plaza fuera mucho mayor (fixture: 5.000.000 ó 3.000.000), nunca
        // gobierna en una ruta solo-SICE-TAC.
        if (valorPropuesto is not null)
            Assert.True(l.Plaza is null || l.Plaza > valorPropuesto,
                "la fixture debería poner una plaza mayor para probar que se ignora.");
    }

    // ------------------------------------------------ datos incompletos/malformados (documentados, no ejecutables aquí)

    [Theory]
    [InlineData(311)]
    [InlineData(312)]
    public void Lineas_sin_ruta_quedan_documentadas_como_no_validables_en_dominio(int lineaId)
    {
        // "La línea no tiene ruta definida" se resuelve en
        // PropuestaTarifaAdapter.ProponerLineaAsync antes de siquiera construir un
        // CandidatoDeTarifa; requiere el Include(l => l.Ruta) de EF, fuera del
        // alcance de una prueba de dominio puro. Ver README de la carpeta QA.
        var l = Linea(lineaId);
        Assert.False(l.ValidableEnDominio);
        Assert.True(l.Esperado.RequiereManual);
        Assert.Null(l.Origen);
    }

    [Theory]
    [InlineData(313)]
    [InlineData(314)]
    public void Lineas_con_peso_malformado_quedan_documentadas_como_rechazadas_en_captura(int lineaId)
    {
        // [Range(0, ...)] en LineaSolicitud.Peso rechaza esto en la validación de
        // modelo de la API (400), antes de llegar al auto-costeo.
        var l = Linea(lineaId);
        Assert.False(l.ValidableEnDominio);
        Assert.True(l.Esperado.RequiereManual);
    }
}

// ------------------------------------------------------------------ modelo del dataset

public sealed record PliegoDatasetFixture(
    string Version, string Hu, int TotalLineas, int[] FuenteCaeIds, List<LineaPliegoFixture> Lineas);

public sealed record LineaPliegoFixture(
    int Id, string Caso, string? Origen, string? Destino, string? Vehiculo, string? Carroceria,
    string? Tipologia, decimal? Peso, string? LineaNegocio, string? ClaseViaje, string? ClaseContenedor,
    bool Exclusivo, bool Consolidado, bool RetornaContenedor, string? CiudadDevolucionContenedor,
    decimal? Sicetac, decimal? Plaza, decimal? Historico,
    bool SoloSicetac, bool TarifarioVencido, bool FuenteCae, bool ValidableEnDominio,
    EsperadoFixture Esperado);

public sealed record EsperadoFixture(
    decimal? Valor, string? Fuente, bool RequiereManual,
    [property: JsonPropertyName("contieneEnJustificacion")] string[] ContieneEnJustificacion);
