using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Domain.Priorizacion.Reglas;

/// <summary>
/// Calcula el score de priorización (0–100) que reemplaza el FIFO.
///
/// Score = suma ponderada de 4 componentes (issue #172/#173 — decisión documentada en
/// docs/decisiones/score-prioridad-4-pesos-vs-solo-urgencia.md tras encontrar que el
/// repo se contradecía a sí mismo: el código calculaba los 4 componentes pero el total
/// descartaba 3 de ellos, dejando <see cref="ParametrosScore"/> con 4 pesos editables
/// por el Admin sin ningún efecto real):
///
///  - Urgencia: rampa lineal inversa al tiempo restante hasta el deadline (vencido = 100).
///  - Valor: escala logarítmica del valor estimado contra un valor de referencia.
///  - Probabilidad: probabilidad de cierre (0–100).
///  - Tipo: licitación pesa más que spot.
///
/// Los 4 pesos (<see cref="ParametrosScore.PesoUrgencia"/> y análogos) son editables por
/// el Admin y suman 1.0 por defecto, así que el total queda en la misma escala 0–100.
/// Función pura y determinista: el "ahora" se inyecta para poder probarla.
/// </summary>
public static class ScorePrioridadCalculator
{
    public static ResultadoScore Calcular(Solicitud solicitud, ParametrosScore p, DateTimeOffset ahora)
    {
        var urgencia = ComponenteUrgencia(solicitud.Deadline, ahora, p.HorizonteHoras);
        var valor = ComponenteValor(solicitud.ValorEstimado, p.ValorReferencia, p.ValorPorDefectoSinValor);
        var probabilidad = ComponenteProbabilidad(solicitud.ProbabilidadCierre, p.ProbabilidadPorDefecto);
        var tipo = ComponenteTipo(solicitud.Tipo, p.PuntajeLicitacion, p.PuntajeSpot);

        var total = Math.Clamp(
            p.PesoUrgencia * urgencia + p.PesoValor * valor + p.PesoProbabilidad * probabilidad + p.PesoTipo * tipo,
            0, 100);

        return new ResultadoScore(
            Redondear(total),
            Redondear(urgencia),
            Redondear(valor),
            Redondear(probabilidad),
            Redondear(tipo));
    }

    /// <summary>Mayor cuanto menos tiempo falte para el deadline. Vencido = 100; muy lejano = 0.</summary>
    private static double ComponenteUrgencia(DateTimeOffset? deadline, DateTimeOffset ahora, double horizonteHoras)
    {
        if (deadline is null) return 50;            // sin deadline: urgencia neutra
        if (horizonteHoras <= 0) return 100;

        var horas = (deadline.Value - ahora).TotalHours;
        if (horas <= 0) return 100;                 // vencido o al límite: máxima urgencia
        if (horas >= horizonteHoras) return 0;      // fuera del horizonte: sin urgencia aún
        return 100.0 * (1.0 - horas / horizonteHoras);
    }

    /// <summary>Escala logarítmica del valor económico contra un valor de referencia.</summary>
    private static double ComponenteValor(decimal? valorEstimado, decimal valorReferencia, double porDefecto)
    {
        if (valorEstimado is null || valorEstimado <= 0) return porDefecto;
        if (valorReferencia <= 0) return 100;

        var score = 100.0 * Math.Log(1 + (double)valorEstimado.Value) / Math.Log(1 + (double)valorReferencia);
        return Math.Clamp(score, 0, 100);
    }

    /// <summary>Probabilidad de cierre normalizada a 0–100 (acepta 0–1 o 0–100).</summary>
    private static double ComponenteProbabilidad(decimal? probabilidad, double porDefecto)
    {
        if (probabilidad is null) return porDefecto;

        var valor = (double)probabilidad.Value;
        if (valor <= 1.0) valor *= 100.0;
        return Math.Clamp(valor, 0, 100);
    }

    private static double ComponenteTipo(TipoSolicitud tipo, double puntajeLicitacion, double puntajeSpot)
        => tipo == TipoSolicitud.Licitacion ? puntajeLicitacion : puntajeSpot;

    private static double Redondear(double x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
}
