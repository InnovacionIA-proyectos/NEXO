using System.Globalization;
using System.Text.RegularExpressions;

namespace Nexo.Application.CasosDeUso.Asistente.Conversacion;

/// <summary>
/// Compuerta contra los precios inventados: encuentra en la respuesta del asistente las cifras que parecen dinero y comprueba que cada
/// una aparece en lo que el asistente tenía delante (resultados de herramientas y mensajes del usuario). Una cifra de dinero sin
/// origen significa que el modelo la calculó o la inventó, y la respuesta no se entrega.
///
/// Una cifra "parece dinero" si lleva <c>$</c>, una unidad (<c>millones</c>, <c>mil</c>) o cae junto a una palabra de precio
/// (<c>valor</c>, <c>tarifa</c>, <c>flete</c>, <c>COP</c>…). Los números sin ese contexto (pesos, kilómetros, códigos DANE, periodos) no se
/// revisan: aparecen por todas partes y no son precios. Un número escrito como "3,5 millones" se acepta si está a menos de 1 % de un valor
/// de las fuentes; una cifra exacta debe coincidir con una de ellas.
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

    /// <summary>
    /// <c>3.500.000</c> y <c>3,500,000</c> son tres millones y medio; <c>3.500.000,50</c> también; <c>3,5</c> y <c>3.5</c> son tres y medio. Un
    /// solo separador seguido de exactamente tres dígitos (<c>3.500</c>) se lee como miles, que es como se escribe en Colombia.
    /// </summary>
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
