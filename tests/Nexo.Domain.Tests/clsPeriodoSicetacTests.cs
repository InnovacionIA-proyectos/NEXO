using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Domain.Tests;

// #300: el piso SICE-TAC solo se usa si su período (AAAAMM) es el del mes y año actuales, sin mes de gracia.
public class clsPeriodoSicetacTests
{
    private static DateTimeOffset Utc(int anio, int mes, int dia, int hora = 12) =>
        new(anio, mes, dia, hora, 0, 0, TimeSpan.Zero);

    [Fact]
    public void El_periodo_actual_es_AAAAMM_del_mes_en_curso()
    {
        Assert.Equal(202610, clsPeriodoSicetac.Actual(Utc(2026, 10, 15)));
        Assert.Equal(202601, clsPeriodoSicetac.Actual(Utc(2026, 1, 1)));
    }

    [Fact]
    public void El_periodo_cargado_del_mes_actual_se_usa_y_no_hay_motivo()
    {
        Assert.True(clsPeriodoSicetac.EsElDelMesActual(202610, Utc(2026, 10, 15)));
        Assert.Null(clsPeriodoSicetac.MotivoNoVigente(202610, Utc(2026, 10, 15)));
    }

    // Sin mes de gracia: el día 1 del mes siguiente, el período anterior ya no sirve.
    [Fact]
    public void El_periodo_del_mes_anterior_no_se_usa_y_el_motivo_dice_los_dos_periodos()
    {
        var ahora = Utc(2026, 11, 1);

        Assert.False(clsPeriodoSicetac.EsElDelMesActual(202610, ahora));
        var motivo = clsPeriodoSicetac.MotivoNoVigente(202610, ahora);
        Assert.NotNull(motivo);
        Assert.Contains("202610", motivo);
        Assert.Contains("202611", motivo);
        Assert.Contains("SICE-TAC no se usa", motivo);
    }

    [Fact]
    public void Mismo_mes_de_otro_anio_tampoco_se_usa()
    {
        Assert.False(clsPeriodoSicetac.EsElDelMesActual(202510, Utc(2026, 10, 15)));
    }

    // Colombia es UTC-5: a las 19:00 del 31 de octubre en Bogotá ya es 1 de noviembre en UTC, pero el mes sigue siendo octubre.
    [Fact]
    public void El_cambio_de_mes_es_a_la_medianoche_de_Colombia_no_de_UTC()
    {
        var finDeOctubreEnBogota = Utc(2026, 11, 1, hora: 03); // 22:00 del 31/oct en Bogotá
        Assert.Equal(202610, clsPeriodoSicetac.Actual(finDeOctubreEnBogota));

        var primeroDeNoviembreEnBogota = Utc(2026, 11, 1, hora: 06); // 01:00 del 1/nov en Bogotá
        Assert.Equal(202611, clsPeriodoSicetac.Actual(primeroDeNoviembreEnBogota));
    }

    // Sin ningún período cargado no es "desactualizado": es que no hay matriz (se trata como siempre: sin piso, sin motivo).
    [Fact]
    public void Sin_periodo_cargado_no_hay_motivo_de_desactualizado()
    {
        Assert.Null(clsPeriodoSicetac.MotivoNoVigente(0, Utc(2026, 10, 15)));
    }
}
