using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Domain.Priorizacion.Reglas;

/// <summary>
/// Score de priorización (0–100): suma ponderada, con los pesos de <see cref="clsParametrosScore"/>, de
/// urgencia (rampa lineal inversa al tiempo hasta el deadline; vencido = 100), valor (escala logarítmica
/// contra una referencia), probabilidad de cierre y tipo (licitación pesa más que spot).
/// Función pura: el "ahora" se inyecta.
/// </summary>
public static class clsScorePrioridadCalculator
{
    public static clsResultadoScore Calcular(clsSolicitud solicitud, clsParametrosScore p, DateTimeOffset ahora)
    {
        var urgencia = ComponenteUrgencia(solicitud.Deadline, ahora, p.HorizonteHoras);
        var valor = ComponenteValor(solicitud.ValorEstimado, p.ValorReferencia, p.ValorPorDefectoSinValor);
        var probabilidad = ComponenteProbabilidad(solicitud.ProbabilidadCierre, p.ProbabilidadPorDefecto);
        var tipo = ComponenteTipo(solicitud.Tipo, p.PuntajeLicitacion, p.PuntajeSpot);

        var total = Math.Clamp(
            p.PesoUrgencia * urgencia + p.PesoValor * valor + p.PesoProbabilidad * probabilidad + p.PesoTipo * tipo,
            0, 100);

        return new clsResultadoScore(
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
