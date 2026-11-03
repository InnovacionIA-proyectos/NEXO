using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

/// <summary>
/// S5-HU-19.2/#282: los 10 casos de la tabla del milestone S5-HU-19 (confirmada por
/// negocio, 2026-10-12), uno por test — el piso SICE-TAC de referencia de la tabla es
/// 2.5 M cuando existe. <see cref="clsReglaDeCosteo.Decidir"/>/
/// <see cref="clsReglaDeCosteo.DecidirConTarifario"/> no cambian (clsReglaDeCosteoTests
/// los sigue cubriendo) — esto prueba solo el enrutamiento nuevo de
/// <see cref="clsReglaDeCosteo.DecidirFlujo"/> hacia esos dos métodos.
/// </summary>
public class clsDecidirFlujoTests
{
    private const decimal Piso = 2_500_000m;
    private static clsCandidatoDeTarifa Sicetac(decimal v = Piso) => new("sicetac", v);
    private static clsCandidatoDeTarifa Plaza(decimal v) => new("plaza", v);
    private static clsCandidatoDeTarifa Historico(decimal v) => new("historico_sap", v);

    // Caso 1: regla Sí, con piso -> SoloSicetac. Plaza/histórico "no se miran": ni
    // siquiera hace falta pasarlos.
    [Fact]
    public void Caso1_regla_con_piso_es_SoloSicetac()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: true, Sicetac(), plazaVigente: null, plazaVencida: null, historico: null);

        Assert.Equal(ModoDecision.SoloSicetac, d.Modo);
        Assert.Equal(Piso, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
    }

    // Caso 2: regla Sí, SIN piso -> va directo al paso 4 (consulta completa), NO al
    // paso 2 (que es explícitamente "sin regla"). Gana la plaza vigente.
    [Fact]
    public void Caso2_regla_sin_piso_cae_a_ConsultaCompleta_y_gana_la_plaza()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: true, sicetac: null, Plaza(3_000_000m), plazaVencida: null, Historico(2_800_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(3_000_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
    }

    // Caso 3: sin regla, plaza vigente ESTRICTAMENTE mayor que el piso -> PlazaVigenteMayor,
    // gana la plaza aunque el histórico sea más alto todavía (ni se mira).
    [Fact]
    public void Caso3_plaza_vigente_mayor_que_el_piso_es_PlazaVigenteMayor_e_ignora_el_historico()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), Plaza(3_000_000m), plazaVencida: null, Historico(3_500_000m));

        Assert.Equal(ModoDecision.PlazaVigenteMayor, d.Modo);
        Assert.Equal(3_000_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
    }

    // Caso 4: plaza vigente pero MENOR o igual al piso -> ConsultaCompleta, regla del
    // mayor de siempre; acá plaza e histórico están bajo el piso, gana el piso.
    [Fact]
    public void Caso4_plaza_vigente_bajo_el_piso_es_ConsultaCompleta_y_gana_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), Plaza(2_000_000m), plazaVencida: null, Historico(2_200_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(Piso, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
    }

    // Caso 5: plaza VENCIDA -> ConsultaCompleta, se descarta la plaza, el histórico
    // sigue compitiendo contra el piso (regla 4 de Decidir) y gana.
    [Fact]
    public void Caso5_plaza_vencida_es_ConsultaCompleta_y_el_historico_compite_contra_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), plazaVigente: null, plazaVencida: Plaza(3_000_000m), Historico(2_800_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(2_800_000m, d.Resultado.Valor);
        Assert.Equal("historico_sap", d.Resultado.Fuente);
    }

    // Caso 6: sin plaza en absoluto (ni vigente ni vencida) -> ConsultaCompleta, mismo
    // resultado que el caso 5 (el histórico compite igual contra el piso).
    [Fact]
    public void Caso6_sin_plaza_es_ConsultaCompleta_y_el_historico_compite_contra_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), plazaVigente: null, plazaVencida: null, Historico(2_800_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(2_800_000m, d.Resultado.Valor);
        Assert.Equal("historico_sap", d.Resultado.Fuente);
    }

    // Caso 7: solo el piso, sin plaza ni histórico -> ConsultaCompleta igual (ya no
    // "atención manual" por regla; ya no hay regla acá, pero el único valor disponible
    // tampoco se auto-costea solo: #282 lo deja pendiente igual que el resto de consulta
    // completa, ver clsCapturarSolicitudCommand).
    [Fact]
    public void Caso7_solo_piso_es_ConsultaCompleta_y_gana_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), plazaVigente: null, plazaVencida: null, historico: null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(Piso, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
    }

    // Caso 8: sin piso, con plaza vigente -> ConsultaCompleta (el paso 2 nunca se evalúa
    // sin piso con el que comparar), gana la plaza.
    [Fact]
    public void Caso8_sin_piso_con_plaza_vigente_es_ConsultaCompleta_y_gana_la_plaza()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, sicetac: null, Plaza(3_000_000m), plazaVencida: null, historico: null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(3_000_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
    }

    // Caso 9: sin piso, sin plaza, solo histórico -> ConsultaCompleta, SIN recomendado
    // (el histórico sin piso nunca se propone solo, regla 6 — queda solo de referencia).
    [Fact]
    public void Caso9_solo_historico_sin_piso_es_ConsultaCompleta_sin_recomendado()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, sicetac: null, plazaVigente: null, plazaVencida: null, Historico(2_800_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Null(d.Resultado.Valor);
        Assert.True(d.Resultado.RequiereTarifaManual);
    }

    // Caso 10: nada de nada -> ConsultaCompleta, sin recomendado.
    [Fact]
    public void Caso10_sin_ninguna_fuente_es_ConsultaCompleta_sin_recomendado()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, sicetac: null, plazaVigente: null, plazaVencida: null, historico: null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Null(d.Resultado.Valor);
        Assert.True(d.Resultado.RequiereTarifaManual);
    }
}
