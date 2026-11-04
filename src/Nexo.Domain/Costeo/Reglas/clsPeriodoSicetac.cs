namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// El piso SICE-TAC solo se usa si su período (AAAAMM) es el del mes y año actuales, para no cotizar contra
/// un piso viejo si DWSice se atrasa. Sin mes de gracia: el día 1 del mes siguiente el período anterior ya no sirve.
/// </summary>
public static class clsPeriodoSicetac
{
    /// <summary>Colombia no tiene horario de verano: UTC-5 fijo. El mes cambia a las 00:00 de Bogotá, no a las 00:00 UTC.</summary>
    private static readonly TimeSpan OffsetColombia = TimeSpan.FromHours(-5);

    /// <summary>Período AAAAMM de <paramref name="ahora"/> en hora de Colombia (p. ej. 202610).</summary>
    public static int Actual(DateTimeOffset ahora)
    {
        var local = ahora.ToOffset(OffsetColombia);
        return local.Year * 100 + local.Month;
    }

    public static bool EsElDelMesActual(int periodo, DateTimeOffset ahora) => periodo == Actual(ahora);

    /// <summary>Motivo visible cuando el período cargado no es el actual. Null si lo es o si no hay período (<paramref name="periodoCargado"/> = 0: no hay matriz, se trata como sin piso).</summary>
    public static string? MotivoNoVigente(int periodoCargado, DateTimeOffset ahora)
    {
        if (periodoCargado == 0 || EsElDelMesActual(periodoCargado, ahora)) return null;
        return $"SICE-TAC no se usa: el período más reciente cargado es {periodoCargado} y el del mes actual es " +
               $"{Actual(ahora)} (sin mes de gracia).";
    }
}
