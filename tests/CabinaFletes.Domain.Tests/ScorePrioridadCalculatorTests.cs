using CabinaFletes.Domain.Entities;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Scoring;

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

    [Fact]
    public void Total_es_exclusivamente_la_urgencia_por_deadline()
    {
        var p = Parametros();
        // Aunque valor/probabilidad/tipo tengan valores altos, el total = urgencia (deadline).
        var r = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(120), 80_000_000m, 0.8m), p, Ahora);

        Assert.Equal(r.Urgencia, r.Total);
        Assert.InRange(r.Total, 0, 100);
    }

    [Fact]
    public void El_total_no_depende_de_valor_probabilidad_ni_tipo()
    {
        var p = Parametros();
        var deadline = Ahora.AddHours(120);
        // Mismo deadline, factores muy distintos: el total debe ser idéntico.
        var a = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, deadline, 80_000_000m, 0.9m), p, Ahora);
        var b = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Spot, deadline, 1_000m, 0.1m), p, Ahora);

        Assert.Equal(a.Total, b.Total);
    }

    [Fact]
    public void Deadline_mas_cercano_supera_a_deadline_mas_lejano()
    {
        var p = Parametros();
        var urgente = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Spot, Ahora.AddHours(12), 1_000m, 0.1m), p, Ahora);
        var holgado = ScorePrioridadCalculator.Calcular(
            Solicitud(TipoSolicitud.Licitacion, Ahora.AddHours(200), 80_000_000m, 0.9m), p, Ahora);

        Assert.True(urgente.Total > holgado.Total);
    }
}
