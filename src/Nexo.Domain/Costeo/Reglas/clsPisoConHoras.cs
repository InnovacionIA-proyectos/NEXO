namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// Recalcula el piso SICE-TAC con las horas de cargue/descargue REALES de la solicitud
/// (S4-HU-14.1/#221), en vez de tomar tal cual el <c>ValorSiceConHora</c> que trae DWSice
/// ya precalculado asumiendo las horas PACTADAS (por defecto 4h+4h) — si el Comercial captura
/// horas distintas, la propuesta automática seguía asumiendo las pactadas, un piso
/// desactualizado frente a lo que el negocio ya sabe.
///
/// Única función que hace esta cuenta en el backend: tanto <c>clsPropuestaTarifaAdapter</c>
/// (propuesta automática) como cualquier otro punto que la necesite deben llamarla en vez
/// de reimplementarla — mismo criterio que ya usa "floorTotal()" en Mesa de Costeo
/// (frontend, fuera de este repo): <c>ValorPiso + ValorHora × horas</c>.
/// </summary>
public static class clsPisoConHoras
{
    /// <summary>
    /// <paramref name="horasSolicitud"/> null = no hay contexto de horas capturadas (p. ej.
    /// la estimación de aprovisionamiento, que no es de una línea de una solicitud) — se usa
    /// el fallback siempre. Con horas, si faltan <paramref name="valorHora"/>/
    /// <paramref name="horasPactadas"/> en la fila de DWSice, o las horas capturadas
    /// coinciden con las pactadas, también se usa el fallback (mismo valor de siempre — sin
    /// regresión). Solo cuando difieren y hay con qué recalcular se sustituye.
    /// </summary>
    public static decimal Calcular(
        decimal valorBase, decimal? valorHora, decimal? horasPactadas, decimal? valorSiceConHora, decimal? horasSolicitud)
    {
        if (horasSolicitud is not { } horas || valorHora is not { } vh || horasPactadas is not { } hp || horas == hp)
            return valorSiceConHora ?? valorBase;

        return valorBase + vh * horas;
    }
}
