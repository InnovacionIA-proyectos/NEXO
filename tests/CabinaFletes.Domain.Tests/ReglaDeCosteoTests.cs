using CabinaFletes.Domain.Costeo.Reglas;

namespace CabinaFletes.Domain.Tests;

/// <summary>
/// Las reglas inviolables del costeo, probadas en aislamiento.
///
/// Hasta ahora vivían dentro de un adaptador de 470 líneas con cuatro conexiones a
/// almacenes externos, caché y control de concurrencia. Probarlas exigía levantar todo
/// eso, así que en la práctica no se probaban.
///
/// Aquí son una función pura sobre candidatos: entra qué encontró cada fuente, sale
/// qué se propone y por qué.
/// </summary>
public class ReglaDeCosteoTests
{
    private static CandidatoDeTarifa Sicetac(decimal v) => new("sicetac", v);
    private static CandidatoDeTarifa Plaza(decimal v) => new("plaza", v);
    private static CandidatoDeTarifa Historico(decimal v) => new("historico_sap", v);

    // ------------------------------------------------ la regla del mayor

    [Fact]
    public void Entre_plaza_y_piso_gana_el_mayor_cuando_la_plaza_es_mas_alta()
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(1_000_000m), Plaza(1_200_000m), historico: null);

        Assert.Equal(1_200_000m, r.Valor);
        Assert.Equal("plaza", r.Fuente);
        Assert.False(r.RequiereTarifaManual);
    }

    [Fact]
    public void Entre_plaza_y_piso_gana_el_piso_cuando_la_plaza_es_mas_baja()
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(1_500_000m), Plaza(900_000m), historico: null);

        // Lo esencial: NUNCA se propone la plaza si está bajo el piso. Cotizar por
        // debajo del piso legal es sanción.
        Assert.Equal(1_500_000m, r.Valor);
        Assert.Equal("sicetac", r.Fuente);
    }

    [Fact]
    public void Empatados_gana_la_plaza_por_ser_la_tarifa_comercial()
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(1_000_000m), Plaza(1_000_000m), historico: null);

        Assert.Equal(1_000_000m, r.Valor);
        Assert.Equal("plaza", r.Fuente);
    }

    [Fact]
    public void La_justificacion_dice_el_porcentaje_de_diferencia()
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(1_000_000m), Plaza(1_100_000m), historico: null);

        // La trazabilidad de fuente es regla inviolable: toda propuesta muestra su origen.
        Assert.Contains("10", r.Justificacion);
        Assert.Contains("plaza", r.Justificacion, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------ una sola fuente

    [Fact]
    public void Sin_piso_comparable_gobierna_la_plaza()
    {
        var r = ReglaDeCosteo.Decidir(sicetac: null, Plaza(800_000m), historico: null);

        Assert.Equal(800_000m, r.Valor);
        Assert.Equal("plaza", r.Fuente);
    }

    [Fact]
    public void Sin_plaza_vigente_gobierna_el_piso()
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(700_000m), plaza: null, historico: null);

        Assert.Equal(700_000m, r.Valor);
        Assert.Equal("sicetac", r.Fuente);
    }

    // ------------------------------------------------ requiere una persona

    [Fact]
    public void Sin_plaza_ni_piso_la_linea_requiere_intervencion_manual()
    {
        var r = ReglaDeCosteo.Decidir(sicetac: null, plaza: null, historico: null);

        // No es un fallo: el servicio hizo su trabajo y la respuesta es que aquí hace
        // falta el Administrador de Tarifas.
        Assert.Null(r.Valor);
        Assert.Null(r.Fuente);
        Assert.True(r.RequiereTarifaManual);
        Assert.Contains("manual", r.Justificacion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void El_historico_se_ofrece_como_referencia_pero_no_se_propone_solo()
    {
        var r = ReglaDeCosteo.Decidir(sicetac: null, plaza: null, Historico(650_000m));

        // El histórico NO se propone por sí solo: es referencia para que una persona
        // decida. Proponerlo sería costear sin saber si respeta el piso vigente.
        Assert.Null(r.Valor);
        Assert.True(r.RequiereTarifaManual);
        Assert.Contains("eferencia", r.Justificacion);
    }

    // ------------------------------------------------ tarifario vencido

    [Fact]
    public void Con_el_tarifario_vencido_la_plaza_se_descarta_y_gobierna_el_piso()
    {
        // Regla inviolable: tarifa vencida se marca y NO se propone. Aunque la plaza
        // vencida fuera más alta, no se puede cotizar con ella.
        var r = ReglaDeCosteo.DecidirConTarifario(
            Sicetac(1_000_000m), Plaza(2_000_000m), historico: null, tarifarioVencido: true);

        Assert.Equal(1_000_000m, r.Valor);
        Assert.Equal("sicetac", r.Fuente);
        Assert.Contains("INACTIVO", r.Justificacion);
    }

    [Fact]
    public void Tarifario_vencido_y_sin_piso_vigente_requiere_intervencion_manual()
    {
        var r = ReglaDeCosteo.DecidirConTarifario(
            sicetac: null, Plaza(2_000_000m), historico: null, tarifarioVencido: true);

        Assert.Null(r.Valor);
        Assert.True(r.RequiereTarifaManual);
    }

    [Fact]
    public void Con_el_tarifario_vigente_la_decision_es_la_normal()
    {
        var conTarifario = ReglaDeCosteo.DecidirConTarifario(
            Sicetac(1_000_000m), Plaza(1_200_000m), historico: null, tarifarioVencido: false);
        var normal = ReglaDeCosteo.Decidir(Sicetac(1_000_000m), Plaza(1_200_000m), historico: null);

        Assert.Equal(normal.Valor, conTarifario.Valor);
        Assert.Equal(normal.Fuente, conTarifario.Fuente);
    }

    // ------------------------------------------------ invariante transversal

    [Theory]
    [InlineData(500_000, 400_000)]
    [InlineData(1_000_000, 999_999)]
    [InlineData(2_000_000, 1)]
    public void Nunca_se_propone_un_valor_por_debajo_del_piso(decimal piso, decimal plaza)
    {
        var r = ReglaDeCosteo.Decidir(Sicetac(piso), Plaza(plaza), historico: null);

        Assert.NotNull(r.Valor);
        Assert.True(r.Valor >= piso, $"Se propuso {r.Valor} con un piso de {piso}.");
    }
}
