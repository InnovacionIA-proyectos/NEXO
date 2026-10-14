using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;

namespace Nexo.Domain.Tests;

/// <summary>
/// S5-HU-20.5/#297: una línea de Cupo (o sin clase de viaje) SIN vehículo nunca se autocostea: sin vehículo
/// el piso SICE-TAC se busca sin filtro y toma el menor valor, y la plaza cae al "mayor valor vigente".
/// No aplica a Contenedor (el vehículo se deduce por peso y clase de contenedor) ni a Carga suelta ni a
/// Extradimensionado (decidido por Sara: solo Cupo).
/// </summary>
public class clsLineaSinVehiculoTests
{
    private const decimal Piso = 2_500_000m;
    private static clsCandidatoDeTarifa Sicetac(decimal v = Piso) => new("sicetac", v);
    private static clsCandidatoDeTarifa Plaza(decimal v) => new("plaza", v);
    private static clsCandidatoDeTarifa Historico(decimal v) => new("historico_sap", v);

    // ------------------------------------------------------------------ cuándo aplica

    [Theory]
    [InlineData(ClaseViaje.Cupo, null, true)]
    [InlineData(ClaseViaje.Cupo, "", true)]
    [InlineData(ClaseViaje.Cupo, "   ", true)]
    [InlineData(null, null, true)]                                  // sin clase de viaje también
    [InlineData(ClaseViaje.Cupo, "C3", false)]                      // con vehículo: normal
    [InlineData(null, "C3", false)]
    [InlineData(ClaseViaje.Contenedor, null, false)]                // el vehículo se deduce por peso y clase de contenedor
    [InlineData(ClaseViaje.CargaSuelta, null, false)]
    [InlineData(ClaseViaje.Extradimensionado, null, false)]
    public void Solo_Cupo_o_sin_clase_de_viaje_y_sin_vehiculo_exige_consulta_completa(ClaseViaje? clase, string? vehiculo, bool esperado) =>
        Assert.Equal(esperado, clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta(clase, vehiculo));

    // ------------------------------------------------------------------ nunca se autocostea

    [Fact]
    public void Con_regla_Solo_SICETAC_y_piso_NO_es_SoloSicetac_va_a_consulta_completa()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: true, Sicetac(), plazaVigente: null, plazaVencida: null, historico: null,
            lineaSinVehiculoQueLoExige: true);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
    }

    [Fact]
    public void Con_plaza_vigente_mayor_que_el_piso_NO_es_PlazaVigenteMayor()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), Plaza(3_000_000m), plazaVencida: null, historico: null,
            lineaSinVehiculoQueLoExige: true);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
    }

    [Fact]
    public void Queda_con_el_motivo_exacto_y_los_candidatos_siguen_saliendo_como_referencia()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), Plaza(3_000_000m), plazaVencida: null, Historico(2_800_000m),
            lineaSinVehiculoQueLoExige: true);

        Assert.Equal("Línea sin vehículo: requiere consulta completa", d.MotivoPendiente);
        Assert.Equal(clsReglaDeCosteo.MotivoLineaSinVehiculo, d.MotivoPendiente);
        // La referencia es la de la regla del mayor de siempre (plaza 3.0M > piso 2.5M), para que el
        // Administrador de Tarifas no empiece de cero: el motor "sigue devolviendo los candidatos".
        Assert.Equal(3_000_000m, d.Resultado.Valor);
        Assert.Equal("plaza", d.Resultado.Fuente);
        Assert.StartsWith(clsReglaDeCosteo.MotivoLineaSinVehiculo, d.Resultado.Justificacion);
    }

    [Fact]
    public void Sin_ningun_candidato_no_hay_valor_de_referencia_pero_igual_queda_pendiente_con_motivo()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, sicetac: null, plazaVigente: null, plazaVencida: null, historico: null,
            lineaSinVehiculoQueLoExige: true);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Null(d.Resultado.Valor);
        Assert.Equal(clsReglaDeCosteo.MotivoLineaSinVehiculo, d.MotivoPendiente);
    }

    [Fact]
    public void Solo_con_piso_la_referencia_es_el_piso()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac: false, Sicetac(), plazaVigente: null, plazaVencida: null, historico: null,
            lineaSinVehiculoQueLoExige: true);

        Assert.Equal(Piso, d.Resultado.Valor);
        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
    }

    // ------------------------------------------------------------------ sin cambios cuando no aplica

    [Fact]
    public void Con_vehiculo_o_sin_el_flag_el_flujo_es_exactamente_el_de_siempre()
    {
        var solo = clsReglaDeCosteo.DecidirFlujo(true, Sicetac(), null, null, null);
        var mayor = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(), Plaza(3_000_000m), null, null);

        Assert.Equal(ModoDecision.SoloSicetac, solo.Modo);
        Assert.Equal(ModoDecision.PlazaVigenteMayor, mayor.Modo);
        Assert.Null(solo.MotivoPendiente);
        Assert.Null(mayor.MotivoPendiente);
    }

    [Fact]
    public void Una_consulta_completa_normal_no_trae_motivo_especifico()
    {
        var d = clsReglaDeCosteo.DecidirFlujo(false, Sicetac(), Plaza(2_000_000m), null, null);

        Assert.Equal(ModoDecision.ConsultaCompleta, d.Modo);
        Assert.Null(d.MotivoPendiente);
    }
}
