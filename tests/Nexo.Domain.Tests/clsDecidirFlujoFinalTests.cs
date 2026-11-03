using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

/// <summary>
/// Los tres caminos de <see cref="clsReglaDeCosteo.DecidirFlujo"/> y cuándo el flete se guarda solo
/// (<see cref="clsDecisionDeFlujo.Autocosteable"/>): con regla Solo-SICETAC y piso, el piso gobierna; con plaza vigente
/// estrictamente mayor que el piso, la plaza gobierna sola; en el resto (proceso completo) gana el valor más alto entre
/// los candidatos que coinciden en vehículo, y si no hay ninguno la línea queda sin valor.
///
/// Los valores son los de seis rutas de referencia con las tres fuentes en distintas combinaciones; la regla es una función
/// pura, así que no dependen de la tarifa vigente de ningún mes.
/// </summary>
public class clsDecidirFlujoFinalTests
{
    private static clsCandidatoDeTarifa Sicetac(decimal v, bool relajado = false) => new("sicetac", v, relajado);
    private static clsCandidatoDeTarifa Plaza(decimal v, bool relajado = false) => new("plaza", v, relajado);
    private static clsCandidatoDeTarifa Historico(decimal v, bool relajado = false) => new("historico_sap", v, relajado);

    [Fact]
    public void Con_solo_el_piso_el_proceso_completo_lo_recomienda_y_se_guarda_solo()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(7_113_964m), null, null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(7_113_964m, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
        Assert.True(d.Autocosteable);
    }

    [Fact]
    public void Con_solo_la_plaza_vigente_el_proceso_completo_la_recomienda_y_se_guarda_sola()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, null, Plaza(3_100_000m), null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(3_100_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
        Assert.True(d.Autocosteable);
    }

    [Fact]
    public void Con_el_piso_por_encima_de_plaza_e_historico_el_proceso_completo_recomienda_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(6_293_830m), Plaza(5_900_000m), null, Historico(5_900_000m));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(6_293_830m, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
        Assert.True(d.Autocosteable);
    }

    [Fact]
    public void Una_plaza_vigente_mayor_que_el_piso_gobierna_sola_y_el_historico_no_cuenta()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(1_680_915m), Plaza(2_800_000m), null, Historico(2_900_000m));

        Assert.Equal(ModoDecision.PlazaVigenteMayor, d.Modo);
        Assert.Equal(2_800_000m, d.Resultado.Valor);
        Assert.True(d.Autocosteable);
    }

    [Fact]
    public void Con_regla_y_con_piso_el_piso_gobierna_solo_y_se_guarda_solo()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(true, Sicetac(4_738_322m), Plaza(9_000_000m), null, Historico(9_500_000m));

        Assert.Equal(ModoDecision.SoloSicetac, d.Modo);
        Assert.Equal(4_738_322m, d.Resultado.Valor);
        Assert.True(d.Autocosteable);
    }

    [Fact]
    public void Sin_ningun_candidato_no_hay_valor_y_la_linea_queda_pendiente()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, null, null, null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Null(d.Resultado.Valor);
        Assert.False(d.Autocosteable);
    }

    [Fact]
    public void Con_solo_la_plaza_vencida_no_hay_valor_y_la_linea_queda_pendiente()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, null, null, Plaza(1_900_000m), null);

        Assert.Null(d.Resultado.Valor);
        Assert.False(d.Autocosteable);
    }

    [Fact]
    public void Un_historico_sin_piso_es_solo_referencia_y_no_se_guarda()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, null, null, null, Historico(2_800_000m));

        Assert.Null(d.Resultado.Valor);
        Assert.False(d.Autocosteable);
    }

    // ------------------------------------------------------------ candidatos con el vehículo relajado

    [Fact]
    public void Plaza_e_historico_con_vehiculo_relajado_no_entran_al_recomendado_y_el_piso_se_guarda()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            false, Sicetac(3_905_665m), Plaza(2_300_000m, relajado: true), null, Historico(5_100_000m, relajado: true));

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(3_905_665m, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
        Assert.True(d.Autocosteable);
        Assert.Contains("Sin coincidencia de vehículo", d.Resultado.Justificacion);
        Assert.Contains("historico_sap", d.Resultado.Justificacion);
    }

    [Fact]
    public void Una_plaza_relajada_mayor_que_el_piso_no_gobierna_sola()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(1_680_915m), Plaza(2_800_000m, relajado: true), null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(1_680_915m, d.Resultado.Valor);
        Assert.Equal("sicetac", d.Resultado.Fuente);
    }

    [Fact]
    public void Un_piso_relajado_no_activa_Solo_SICETAC_aunque_haya_regla()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(true, Sicetac(4_000_000m, relajado: true), Plaza(3_000_000m), null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(3_000_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
    }

    [Fact]
    public void Si_todos_los_candidatos_estan_relajados_no_hay_valor_y_se_muestran_como_referencia()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(3_000_000m, relajado: true), Plaza(3_500_000m, relajado: true), null, null);

        Assert.Null(d.Resultado.Valor);
        Assert.False(d.Autocosteable);
        Assert.Contains("Sin coincidencia de vehículo", d.Resultado.Justificacion);
    }

    // ------------------------------------------------------------ motivos de pendiente

    [Fact]
    public void Un_motivo_de_pendiente_impide_guardar_aunque_haya_valor()
    {
        var d = new clsDecisionDeFlujo(
            new clsResultadoDeCosteo(5_600_000m, "plaza", "La plaza vigente supera el piso."), ModoDecision.PlazaVigenteMayor,
            MotivoPendiente: "No se encontró piso SICE-TAC de viaje vacío.");

        Assert.False(d.Autocosteable);
    }

    [Fact]
    public void Una_linea_de_Cupo_sin_vehiculo_queda_pendiente_con_su_motivo_aunque_haya_candidatos()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            false, Sicetac(2_500_000m), Plaza(3_000_000m), null, null, lineaSinVehiculoQueLoExige: true);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Equal(clsReglaDeCosteo.MotivoLineaSinVehiculo, d.MotivoPendiente);
        Assert.False(d.Autocosteable);
    }

    [Fact]
    public void Los_valores_del_tramo_vacio_viajan_en_la_decision()
    {
        var d = new clsDecisionDeFlujo(
            new clsResultadoDeCosteo(4_738_322m, "sicetac", "x"), ModoDecision.SoloSicetac,
            MotivoPendiente: null, ValorAprovisionamiento: 2_110_711m, ValorRetornoContenedor: 2_547_021m);

        Assert.Equal(2_110_711m, d.ValorAprovisionamiento);
        Assert.Equal(2_547_021m, d.ValorRetornoContenedor);
        Assert.True(d.Autocosteable);
    }
}
