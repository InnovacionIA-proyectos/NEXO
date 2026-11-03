namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// #300: el piso SICE-TAC solo se usa si su período (AAAAMM) es el del MES Y AÑO ACTUALES. Antes se
/// tomaba el período más alto cargado en DWSice sin compararlo con hoy: si el Data Warehouse se
/// quedaba un mes atrás, el costeo seguía cotizando contra un piso viejo sin que nadie lo notara.
/// Sin mes de gracia: el día 1 del mes siguiente, el período anterior ya no sirve.
///
/// Función pura (no consulta nada): el adaptador le pasa el período cargado y la hora actual.
/// </summary>
public static class clsPeriodoSicetac
{
    /// <summary>Colombia no tiene horario de verano: UTC-5 fijo. El mes cambia a las 00:00 de Bogotá,
    /// no a las 00:00 UTC (a las 19:00 del último día del mes, en UTC ya sería el mes siguiente).</summary>
    private static readonly TimeSpan OffsetColombia = TimeSpan.FromHours(-5);

    /// <summary>Período AAAAMM de <paramref name="ahora"/> en hora de Colombia (p. ej. 202610).</summary>
    public static int Actual(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(OffsetColombia);
        return local.Year * 100 + local.Month;
    }

    public static bool EsElDelMesActual(int periodo, DateTimeOffset ahora) => periodo == Actual(ahora);

    /// <summary>
    /// Motivo visible cuando el período más reciente cargado NO es el actual; null si lo es, o si no hay
    /// ningún período cargado (<paramref name="periodoCargado"/> = 0: no es "desactualizado", es que no hay
    /// matriz, y eso ya se trata como siempre — sin piso).
    /// </summary>
    public static string? MotivoNoVigente(int periodoCargado, DateTimeOffset ahora)
    {
        if (periodoCargado == 0 || EsElDelMesActual(periodoCargado, ahora)) return null;
        return $"SICE-TAC no se usa: el período más reciente cargado es {periodoCargado} y el del mes actual es " +
               $"{Actual(ahora)} (sin mes de gracia).";
    }
}
