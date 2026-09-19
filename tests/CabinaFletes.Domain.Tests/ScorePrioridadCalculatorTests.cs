using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Priorizacion.Reglas;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Domain.Tests;

public class ScorePrioridadCalculatorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    private static ParametrosScore Parametros() => new();

    private static Solicitud Solicitud(
        TipoSolicitud tipo = TipoSolicitud.Spot,
        DateTimeOffset? deadline = null,
        decimal? valor = null,
        decimal? probabilidad = null)
        => new()
        {
            Tipo = tipo,
            Deadline = deadline,
            ValorEstimado = valor,
            ProbabilidadCierre = probabilidad
        };

    [Fact]
    public void Deadline_vencido_da_urgencia_maxima()
    {
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(deadline: Ahora.AddHours(-5)), Parametros(), Ahora);

        Assert.Equal(100, r.Urgencia);
    }

    [Fact]
    public void Deadline_fuera_del_horizonte_da_urgencia_cero()
    {
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(deadline: Ahora.AddHours(500)), Parametros(), Ahora);

        Assert.Equal(0, r.Urgencia);
    }

    [Fact]
    public void Urgencia_a_mitad_del_horizonte_es_cincuenta()
    {
        // Horizonte default 240h -> 120h restantes => 50.
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(deadline: Ahora.AddHours(120)), Parametros(), Ahora);

        Assert.Equal(50, r.Urgencia);
    }

    [Fact]
    public void Licitacion_pesa_mas_que_spot_en_el_componente_tipo()
    {
        var lic = ScorePrioridadCalculator.Calcular(Solicitud(TipoSolicitud.Licitacion), Parametros(), Ahora);
        var spot = ScorePrioridadCalculator.Calcular(Solicitud(TipoSolicitud.Spot), Parametros(), Ahora);

        Assert.Equal(100, lic.Tipo);
        Assert.Equal(50, spot.Tipo);
    }

    [Fact]
    public void Probabilidad_acepta_escala_0_a_1_y_0_a_100()
    {
        var comoFraccion = ScorePrioridadCalculator.Calcular(Solicitud(probabilidad: 0.8m), Parametros(), Ahora);
        var comoPorcentaje = ScorePrioridadCalculator.Calcular(Solicitud(probabilidad: 80m), Parametros(), Ahora);

        Assert.Equal(80, comoFraccion.Probabilidad);
        Assert.Equal(80, comoPorcentaje.Probabilidad);
    }

    [Fact]
    public void Sin_valor_usa_el_puntaje_por_defecto()
    {
        var r = ScorePrioridadCalculator.Calcular(Solicitud(valor: null), Parametros(), Ahora);

        Assert.Equal(Parametros().ValorPorDefectoSinValor, r.Valor);
    }

    [Fact]
    public void Mayor_valor_economico_produce_mayor_componente_valor()
    {
        var bajo = ScorePrioridadCalculator.Calcular(Solicitud(valor: 1_000_000m), Parametros(), Ahora);
        var alto = ScorePrioridadCalculator.Calcular(Solicitud(valor: 80_000_000m), Parametros(), Ahora);

        Assert.True(alto.Valor > bajo.Valor);
    }

    /// <summary>
    /// Issue #172/#173: el total ERA exclusivamente la urgencia (defecto documentado en
    /// docs/decisiones/score-prioridad-4-pesos-vs-solo-urgencia.md) - ahora es la suma
    /// ponderada de los 4 componentes, con los pesos de ParametrosScore.
    /// </summary>
    [Fact]
    public void Total_es_la_suma_ponderada_de_los_4_componentes()
    {
        var p = Parametros();
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(120), 80_000_000m, 0.8m), p, Ahora);

        var esperado = Math.Round(
            p.PesoUrgencia * r.Urgencia + p.PesoValor * r.Valor + p.PesoProbabilidad * r.Probabilidad + p.PesoTipo * r.Tipo,
            2, MidpointRounding.AwayFromZero);
        Assert.Equal(esperado, r.Total);
        Assert.InRange(r.Total, 0, 100);
    }

    [Fact]
    public void El_total_SI_depende_de_valor_probabilidad_y_tipo()
    {
        var p = Parametros();
        var deadline = Ahora.AddHours(120);
        // Mismo deadline (misma urgencia), factores muy distintos: el total ya NO debe
        // ser idéntico - es exactamente el empate masivo que reportó el issue #174
        // (oportunidades de $9M y $3.9M con el mismo score solo por compartir urgencia).
        var valiosa = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, deadline, 80_000_000m, 0.9m), p, Ahora);
        var modesta = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Spot, deadline, 1_000m, 0.1m), p, Ahora);

        Assert.Equal(valiosa.Urgencia, modesta.Urgencia); // misma urgencia...
        Assert.NotEqual(valiosa.Total, modesta.Total);    // ...pero el total ya distingue el resto
        Assert.True(valiosa.Total > modesta.Total, "la oportunidad de mayor valor/probabilidad/tipo debe rankear más alto");
    }

    [Fact]
    public void Con_pesos_en_cero_para_valor_probabilidad_y_tipo_el_total_vuelve_a_ser_solo_urgencia()
    {
        // El Admin puede recalibrar los pesos (PATCH /api/priorizacion/parametros) - con
        // los otros 3 en 0, el comportamiento converge al caso "solo urgencia" de antes,
        // confirmando que la fórmula nueva es una generalización, no una ruptura.
        var p = new ParametrosScore { PesoUrgencia = 1.0, PesoValor = 0, PesoProbabilidad = 0, PesoTipo = 0 };
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(120), 80_000_000m, 0.9m), p, Ahora);

        Assert.Equal(r.Urgencia, r.Total);
    }

    [Fact]
    public void Deadline_mas_cercano_supera_a_deadline_mas_lejano_a_igualdad_de_lo_demas()
    {
        // Antes del fix (#173) esto se probaba mezclando deadline con valor/probabilidad/tipo
        // a la vez, lo cual con la suma ponderada ya no aísla la urgencia: una licitación de
        // $80M al 90% de probabilidad SI puede superar a un spot de $1.000 aunque su deadline
        // esté más lejos (es justo el comportamiento que pide la HU). Para probar el efecto de
        // la urgencia hay que mantener valor/probabilidad/tipo iguales y variar solo el deadline.
        var p = Parametros();
        var urgente = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(12), 80_000_000m, 0.9m), p, Ahora);
        var holgado = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(200), 80_000_000m, 0.9m), p, Ahora);

        Assert.True(urgente.Total > holgado.Total);
    }

    [Fact]
    public void Una_licitacion_de_mayor_valor_y_probabilidad_puede_superar_a_un_spot_mas_urgente()
    {
        // Caso real que motivó la HU (issue #174): con la fórmula vieja (solo urgencia) esto
        // era imposible sin importar cuánto valiera la oportunidad. Con los 4 pesos, sí puede.
        var p = Parametros();
        var spotUrgente = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Spot, Ahora.AddHours(12), 1_000m, 0.1m), p, Ahora);
        var licitacionHolgada = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(200), 80_000_000m, 0.9m), p, Ahora);

        Assert.True(licitacionHolgada.Total > spotUrgente.Total);
    }
}
