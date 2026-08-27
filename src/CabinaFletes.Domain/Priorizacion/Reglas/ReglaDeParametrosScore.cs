using CabinaFletes.Domain.Priorizacion.Entidades;

namespace CabinaFletes.Domain.Priorizacion.Reglas;

/// <summary>
/// Qué tiene que cumplir una recalibración del score para poder guardarse.
///
/// OJO CON LOS PESOS: HOY NO ENTRAN EN EL TOTAL. Por decisión de negocio,
/// <see cref="ScorePrioridadCalculator"/> devuelve como total EXCLUSIVAMENTE el
/// componente de urgencia por deadline; valor, probabilidad y tipo se calculan solo para
/// el desglose. O sea que <c>PesoUrgencia</c>, <c>PesoValor</c>, <c>PesoProbabilidad</c>
/// y <c>PesoTipo</c> son configuración inerte: cambiarlos no mueve ningún puntaje.
///
/// Se comprobó levantando la aplicación: subir PesoUrgencia de 0.40 a 0.60 recalculó las
/// 12 solicitudes activas y actualizó CERO. El único parámetro que mueve el total es
/// <c>HorizonteHoras</c> — cambiarlo de 240 a 4000 llevó una solicitud a 30 días de su
/// deadline de 0.00 a 82.00.
///
/// Entonces por qué exigir que sumen 1.0: porque el día que el total vuelva a ser una
/// suma ponderada —el comentario de la entidad y los nombres de los campos dicen que
/// alguna vez lo fue— una configuración con pesos que suman 1.3 haría que el score dejara
/// de estar en escala 0–100 y los puntajes de antes y después no se pudieran comparar. La
/// regla mantiene coherente lo que hoy está dormido, en vez de dejar que se degrade.
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
                $"Los pesos deben sumar 1.0 y suman {suma:0.###}. Hoy no afectan el total —que es " +
                "solo la urgencia por deadline— pero se mantienen coherentes para cuando vuelvan a " +
                "ponderarse; con otra suma el score dejaría de estar en escala 0–100.");

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
