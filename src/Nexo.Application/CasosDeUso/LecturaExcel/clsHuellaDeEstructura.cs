using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.LecturaExcel;

/// <summary>
/// La «huella» de la estructura de un archivo: los títulos y rótulos que están por encima de los datos de cada hoja que usa la receta, más el nombre de
/// esas hojas. Sirve para reconocer que un archivo nuevo del cliente tiene el mismo formato que uno ya confirmado (aunque cambien las filas de datos) y para
/// avisar qué cambió cuando no es igual. Solo trae texto de la parte alta de la hoja; nunca valores numéricos.
/// </summary>
public static class clsHuellaDeEstructura
{
    public const double UmbralIgual = 0.85;
    public const double UmbralParecido = 0.5;

    private const int MaxTokens = 400;
    private const int MaxColumnas = 40;
    private const int FilasPorDefecto = 10;
    private const int MaxFilas = 60;

    private static readonly Regex SoloNumeros = new(@"^[\d\s.,$%\-/:]+$", RegexOptions.Compiled);

    /// <summary>
    /// Los tokens de las hojas que la receta usa. Cada hoja aporta su nombre (si la receta la nombra) y los textos de las filas hasta la última fila de títulos
    /// o de constantes que la receta menciona (hasta 60 filas), sin contar los datos que vienen debajo.
    /// </summary>
    public static IReadOnlyList<string> Tokens(IReadOnlyList<clsHojaCruda> hojas, clsRecetaLectura receta)
    {
        var tokens = new HashSet<string>();
        foreach (var hoja in hojas)
        {
            if (clsEjecutorDeReceta.EntradaDe(receta, hoja) is not { Usar: true } entrada) continue;
            if (entrada.Hoja.Trim() != "*") tokens.Add("h:" + clsResolucionDeCatalogos.Normalizar(hoja.Nombre));

            var g = entrada.Trasponer ? hoja.Trasponer() : hoja;
            var limite = LimiteDeFilas(entrada);
            for (var f = 1; f <= Math.Min(limite, g.Filas); f++)
                for (var c = 1; c <= Math.Min(g.Columnas, MaxColumnas); c++)
                {
                    var t = g.ValorEfectivo(f, c).Trim();
                    if (t.Length < 2 || SoloNumeros.IsMatch(t)) continue;
                    var norm = clsResolucionDeCatalogos.Normalizar(t);
                    if (norm.Length is < 2 or > 80) continue;
                    tokens.Add("t:" + norm);
                    if (tokens.Count >= MaxTokens) return tokens.Order().ToList();
                }
        }
        return tokens.Order().ToList();
    }

    private static int LimiteDeFilas(clsRecetaHoja h)
    {
        var filas = new List<int>();
        filas.AddRange(h.FilasEncabezado ?? []);
        filas.AddRange(h.Valores?.Dimensiones?.Select(d => d.Fila) ?? []);
        filas.AddRange((h.Constantes ?? []).Select(c => clsReferenciasA1.ACelda(c.Celda)?.Fila ?? 0));
        var max = filas.Count == 0 ? FilasPorDefecto : filas.Max();
        return Math.Clamp(max, 1, MaxFilas);
    }

    /// <summary>Parecido entre dos huellas, de 0 a 1 (índice de Jaccard). Dos huellas vacías no se parecen a nada.</summary>
    public static double Similitud(IReadOnlyCollection<string> a, IReadOnlyCollection<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var sa = a as ISet<string> ?? a.ToHashSet();
        var comunes = b.Count(sa.Contains);
        var union = sa.Count + b.Distinct().Count() - comunes;
        return union == 0 ? 0 : (double)comunes / union;
    }

    /// <summary>Qué títulos ya no aparecen y cuáles son nuevos respecto a la huella guardada (para decirle a la persona qué cambió).</summary>
    public static (IReadOnlyList<string> Faltan, IReadOnlyList<string> Nuevos) Diferencias(IEnumerable<string> guardada, IEnumerable<string> actual, int max = 8)
    {
        var g = guardada.ToHashSet();
        var a = actual.ToHashSet();
        string Legible(string t) => t[(t.IndexOf(':') + 1)..];
        return (g.Except(a).Order().Take(max).Select(Legible).ToList(), a.Except(g).Order().Take(max).Select(Legible).ToList());
    }
}
