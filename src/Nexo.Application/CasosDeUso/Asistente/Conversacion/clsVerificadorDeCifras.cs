using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>
/// Descarta respuestas con cifras de dinero (con <c>$</c>, una unidad o junto a una palabra de precio) que no están en las fuentes.
/// "3,5 millones" se acepta a menos de 1 % de una fuente; una cifra exacta debe coincidir.
/// </summary>
public static partial class clsVerificadorDeCifras
{
    private const int VentanaDeContexto = 30;

    [GeneratedRegex(@"(?<signo>\$\s*)?(?<num>\d{1,3}(?:[.,]\d{3})+(?:[.,]\d{1,2})?|\d+(?:[.,]\d+)?)(?<un>\s*(?:millones|millón|millon|mil)\b)?", RegexOptions.IgnoreCase)]
    private static partial Regex NumeroRegex();

    [GeneratedRegex(@"\b(valor|valores|tarifa|tarifas|flete|fletes|precio|precios|costo|costos|cobr\w*|piso|pesos|cop|total|plaza|hist[oó]ric\w*|sice-?tac|propuest\w*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PalabraDePrecioRegex();

    /// <summary>Las cifras de dinero de <paramref name="respuesta"/> que no están en ninguna de <paramref name="fuentes"/>, tal como se escribieron.</summary>
    public static IReadOnlyList<string> CifrasSinOrigen(string respuesta, IEnumerable<string> fuentes)
    {
        var permitidas = fuentes.SelectMany(f => Numeros(f)).Select(n => n.Valor).Distinct().ToList();

        return Numeros(respuesta)
            .Where(n => n.PareceDinero(respuesta))
            .Where(n => !permitidas.Any(p => Coincide(n, p)))
            .Select(n => n.Texto)
            .Distinct()
            .ToList();
    }

    private static bool Coincide(clsNumero n, decimal permitido) =>
        n.ConUnidad
            ? Math.Abs(n.Valor - permitido) <= Math.Max(1m, Math.Abs(permitido) * 0.01m)
            : n.Valor == permitido || n.Valor == Math.Round(permitido, MidpointRounding.AwayFromZero);

    private sealed record clsNumero(string Texto, decimal Valor, bool ConSigno, bool ConUnidad, int Inicio, int Fin)
    {
        public bool PareceDinero(string texto)
        {
            if (ConSigno || ConUnidad) return true;
            if (Valor < 1000m) return false; // una cifra pequeña no es un valor de flete

            var desde = Math.Max(0, Inicio - VentanaDeContexto);
            var hasta = Math.Min(texto.Length, Fin + VentanaDeContexto);
            return PalabraDePrecioRegex().IsMatch(texto.AsSpan(desde, hasta - desde).ToString());
        }
    }

    private static IEnumerable<clsNumero> Numeros(string texto)
    {
        foreach (Match m in NumeroRegex().Matches(texto))
        {
            var valor = Interpretar(m.Groups["num"].Value);
            if (valor is null) continue;

            var unidad = m.Groups["un"].Success ? m.Groups["un"].Value.Trim().ToLowerInvariant() : null;
            var multiplo = unidad switch { null => 1m, "mil" => 1_000m, _ => 1_000_000m };
            yield return new clsNumero(m.Value.Trim(), valor.Value * multiplo, m.Groups["signo"].Success, unidad is not null, m.Index, m.Index + m.Length);
        }
    }

    /// <summary>Convierte a número tolerando separadores: un solo separador con tres dígitos (<c>3.500</c>) son miles, como en Colombia.</summary>
    private static decimal? Interpretar(string crudo)
    {
        var separadores = crudo.Count(c => c is '.' or ',');
        string normalizado;

        if (separadores == 0)
            normalizado = crudo;
        else if (Regex.IsMatch(crudo, @"^\d{1,3}([.,]\d{3})+$"))
            normalizado = Regex.Replace(crudo, @"[.,]", string.Empty);
        else
        {
            var ultimo = crudo.LastIndexOfAny(['.', ',']);
            var entera = Regex.Replace(crudo[..ultimo], @"[.,]", string.Empty);
            normalizado = $"{entera}.{crudo[(ultimo + 1)..]}";
        }

        return decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : null;
    }
}
