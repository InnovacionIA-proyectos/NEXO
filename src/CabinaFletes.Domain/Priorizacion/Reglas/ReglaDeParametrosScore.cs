using CabinaFletes.Domain.Priorizacion.Entidades;

namespace CabinaFletes.Domain.Priorizacion.Reglas;

/// <summary>
/// Qué tiene que cumplir una recalibración del score para poder guardarse.
///
/// Desde el fix de issue #173 (ver
/// docs/decisiones/score-prioridad-4-pesos-vs-solo-urgencia.md), los 4 pesos SÍ entran
/// en el total: <see cref="ScorePrioridadCalculator"/> devuelve
/// <c>PesoUrgencia·urgencia + PesoValor·valor + PesoProbabilidad·probabilidad + PesoTipo·tipo</c>.
/// Por eso deben sumar 1.0: con otra suma el score dejaría de estar en escala 0–100 y los
/// puntajes de antes y después de recalibrar no se podrían comparar.
///
/// Vive en Domain porque es una regla del negocio, no del endpoint. Devuelve los motivos
/// en vez de lanzar: quién los convierte en un 400 es decisión de Application.
/// </summary>
public static class ReglaDeParametrosScore
{
    /// <summary>
    /// Tolerancia al comparar la suma de pesos.
    ///
    /// Existe porque son <c>double</c>: 0.40 + 0.30 + 0.20 + 0.10 no da exactamente 1.0
    /// en binario. Rechazar por eso sería rechazar la configuración por defecto.
    /// </summary>
    public const double ToleranciaDeSuma = 0.001;

    public static IReadOnlyList<string> Validar(ParametrosScore p)
    {
        var motivos = new List<string>();

        var suma = p.PesoUrgencia + p.PesoValor + p.PesoProbabilidad + p.PesoTipo;
        if (Math.Abs(suma - 1.0) > ToleranciaDeSuma)
            motivos.Add(
                $"Los pesos deben sumar 1.0 y suman {suma:0.###}. Determinan directamente el " +
                "score de prioridad; con otra suma el score dejaría de estar en escala 0–100.");

        foreach (var (nombre, peso) in new[]
                 {
                     ("PesoUrgencia", p.PesoUrgencia), ("PesoValor", p.PesoValor),
                     ("PesoProbabilidad", p.PesoProbabilidad), ("PesoTipo", p.PesoTipo)
                 })
            if (peso is < 0 or > 1)
                motivos.Add($"{nombre} debe estar entre 0 y 1.");

        // El horizonte divide al calcular la urgencia: en cero o negativo el componente
        // se va a infinito o cambia de signo.
        if (p.HorizonteHoras <= 0)
            motivos.Add("HorizonteHoras debe ser mayor que cero: es el divisor de la urgencia.");

        // La referencia normaliza el componente económico en escala logarítmica.
        if (p.ValorReferencia <= 0)
            motivos.Add("ValorReferencia debe ser mayor que cero: normaliza el componente económico.");

        foreach (var (nombre, puntaje) in new[]
                 {
                     ("PuntajeLicitacion", p.PuntajeLicitacion), ("PuntajeSpot", p.PuntajeSpot),
                     ("ValorPorDefectoSinValor", p.ValorPorDefectoSinValor),
                     ("ProbabilidadPorDefecto", p.ProbabilidadPorDefecto)
                 })
            if (puntaje is < 0 or > 100)
                motivos.Add($"{nombre} debe estar entre 0 y 100.");

        return motivos;
    }
}
