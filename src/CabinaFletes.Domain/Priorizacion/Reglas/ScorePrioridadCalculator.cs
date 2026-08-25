using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Domain.Priorizacion.Reglas;

/// <summary>
/// Calcula el score de priorización (0–100) que reemplaza el FIFO.
///
/// Score = Urgencia (EXCLUSIVAMENTE en función de la fecha límite / deadline).
///
/// Por decisión de negocio, la prioridad depende solo de qué tan cerca está el deadline:
///  - Urgencia: rampa lineal inversa al tiempo restante hasta el deadline (vencido = 100).
///
/// Los demás componentes (valor, probabilidad de cierre, tipo) se siguen calculando SOLO
/// como referencia en el desglose de <see cref="ResultadoScore"/>, pero NO afectan el total.
/// El horizonte de la rampa vive en <see cref="ParametrosScore"/> (editable por el Admin).
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

        // El total es EXCLUSIVAMENTE la urgencia por deadline. Valor/probabilidad/tipo
        // quedan en el desglose como referencia, sin ponderarse en el total.
        var total = urgencia;

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
