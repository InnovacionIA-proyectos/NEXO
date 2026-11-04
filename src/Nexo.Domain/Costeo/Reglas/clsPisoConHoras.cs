namespace Nexo.Domain.Costeo.Reglas;

/// <summary>
/// Recalcula el piso SICE-TAC con las horas de cargue/descargue reales de la solicitud, en vez de usar el
/// <c>ValorSiceConHora</c> que DWSice precalcula con las horas pactadas (4h+4h por defecto):
/// <c>ValorPiso + ValorHora × horas</c>. Es la única implementación de esta cuenta en el backend.
/// </summary>
public static class clsPisoConHoras
{
    /// <summary>
    /// <paramref name="horasSolicitud"/> null = sin horas capturadas (p. ej. estimación de aprovisionamiento): siempre el fallback.
    /// También se usa el fallback si faltan <paramref name="valorHora"/>/<paramref name="horasPactadas"/> o las horas coinciden
    /// con las pactadas; solo se recalcula cuando difieren y hay con qué.
    /// </summary>
    public static decimal Calcular(
        decimal valorBase, decimal? valorHora, decimal? horasPactadas, decimal? valorSiceConHora, decimal? horasSolicitud)
    {
        if (horasSolicitud is not { } horas || valorHora is not { } vh || horasPactadas is not { } hp || horas == hp)
            return valorSiceConHora ?? valorBase;

        return valorBase + vh * horas;
    }
}
