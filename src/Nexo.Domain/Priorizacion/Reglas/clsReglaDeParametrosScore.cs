using Nexo.Domain.Priorizacion.Entidades;

namespace Nexo.Domain.Priorizacion.Reglas;

/// <summary>
/// Qué debe cumplir una recalibración del score para guardarse. Los 4 pesos deben sumar 1.0: con otra suma el
/// score sale de la escala 0–100 y los puntajes antes y después no serían comparables. Devuelve motivos en vez de lanzar.
/// </summary>
public static class clsReglaDeParametrosScore
{
    /// <summary>Tolerancia de la suma de pesos: son <c>double</c> y 0.40 + 0.30 + 0.20 + 0.10 no da exactamente 1.0.</summary>
    public const double ToleranciaDeSuma = 0.001;

    public static IReadOnlyList<string> Validar(clsParametrosScore p)
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

        // El horizonte divide la urgencia: en cero o negativo el componente se dispara o cambia de signo.
        if (p.HorizonteHoras <= 0)
            motivos.Add("HorizonteHoras debe ser mayor que cero: es el divisor de la urgencia.");

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
