using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.MapeoExcel.Normalizacion;

/// <summary>
/// Cercanía entre dos nombres escritos distinto («Bogota D.C.» y «Bogotá, D.C.»). Es solo para ELEGIR qué candidatos se le muestran al modelo y
/// para reconocer un nombre idéntico con otra puntuación; lo que se asigna lo decide una regla exacta, nunca este puntaje.
/// </summary>
public static class clsSimilitudDeTexto
{
    private static readonly HashSet<string> Vacias = ["DE", "DEL", "LA", "LAS", "LOS", "Y", "DC"];

    /// <summary>Las palabras que identifican un nombre: sin tildes, sin puntuación y sin artículos ni «D.C.».</summary>
    public static IReadOnlyList<string> Palabras(string? texto) =>
        Regex.Split(clsResolucionDeCatalogos.Normalizar(texto).Replace(".", string.Empty), @"[^A-Z0-9]+")
            .Where(p => p.Length > 0 && !Vacias.Contains(p))
            .ToList();

    /// <summary>Dos nombres con las mismas palabras (en cualquier orden y sin importar la puntuación) tienen la misma clave.</summary>
    public static string Clave(string? texto) => string.Join(' ', Palabras(texto).Distinct().Order(StringComparer.Ordinal));

    /// <summary>De 0 a 1. 1 = mismas palabras; 0.85–0.95 = uno contiene al otro; si no, parecido de letras.</summary>
    public static double Puntaje(string? a, string? b)
    {
        var pa = Palabras(a);
        var pb = Palabras(b);
        if (pa.Count == 0 || pb.Count == 0) return 0;
        if (Clave(a) == Clave(b)) return 1;

        var conjuntoA = pa.ToHashSet();
        var conjuntoB = pb.ToHashSet();
        if (conjuntoA.IsSubsetOf(conjuntoB) || conjuntoB.IsSubsetOf(conjuntoA))
            return 0.85 + 0.1 * Math.Min(conjuntoA.Count, conjuntoB.Count) / Math.Max(conjuntoA.Count, conjuntoB.Count);

        var sa = string.Join(' ', pa);
        var sb = string.Join(' ', pb);
        return 1.0 - (double)Distancia(sa, sb) / Math.Max(sa.Length, sb.Length);
    }

    /// <summary>Los mejores candidatos con al menos <paramref name="minimo"/> de puntaje, del más al menos parecido.</summary>
    public static IReadOnlyList<T> MejoresCandidatos<T>(string texto, IEnumerable<T> candidatos, Func<T, string> nombre, int max, double minimo) =>
        candidatos
            .Select(c => (Candidato: c, Puntaje: Puntaje(texto, nombre(c))))
            .Where(x => x.Puntaje >= minimo)
            .OrderByDescending(x => x.Puntaje).ThenBy(x => nombre(x.Candidato), StringComparer.Ordinal)
            .Take(max)
            .Select(x => x.Candidato)
            .ToList();

    private static int Distancia(string a, string b)
    {
        var anterior = new int[b.Length + 1];
        var actual = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) anterior[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            actual[0] = i;
            for (var j = 1; j <= b.Length; j++)
                actual[j] = Math.Min(Math.Min(actual[j - 1] + 1, anterior[j] + 1), anterior[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (anterior, actual) = (actual, anterior);
        }
        return anterior[b.Length];
    }
}
