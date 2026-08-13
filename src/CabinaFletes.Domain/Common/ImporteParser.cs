using System.Globalization;

namespace CabinaFletes.Domain.Common;

/// <summary>
/// Parser robusto de importes/pesos que llegan como TEXTO desde fuentes con formato
/// heterogéneo (export de SAP, Excel de SICE-TAC). Interpreta tanto el formato
/// colombiano (coma decimal, punto de miles: "1.500.000,50") como el invariante
/// ("1500000.50" / "1,500,000.50"), detectando el separador decimal como el que
/// aparece más a la derecha. Reemplaza el uso de <c>NumberStyles.Any</c> con
/// <c>InvariantCulture</c>, que interpretaba "1500000,50" como 150.000.050 (×100)
/// y descartaba en silencio "1.500.000".
/// </summary>
public static class ImporteParser
{
    public static decimal? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Conservar solo dígitos, signo y separadores (quita "$", NBSP, letras…).
        var limpio = new string(raw.Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+').ToArray());
        if (limpio.Length == 0) return null;

        var negativo = limpio.TrimStart('+').StartsWith('-');
        limpio = limpio.Replace("+", string.Empty).Replace("-", string.Empty);
        if (limpio.Length == 0) return null;

        var puntos = limpio.Count(c => c == '.');
        var comas = limpio.Count(c => c == ',');

        if (puntos > 0 && comas > 0)
        {
            // Ambos presentes: el separador decimal es el que está más a la derecha.
            var dec = limpio.LastIndexOf('.') > limpio.LastIndexOf(',') ? '.' : ',';
            var miles = dec == '.' ? ',' : '.';
            limpio = limpio.Replace(miles.ToString(), string.Empty).Replace(dec, '.');
        }
        else if (comas > 0)
        {
            // Solo comas: una sola coma con pinta decimal => decimal; el resto => miles.
            limpio = comas == 1 && !EsAgrupacionMiles(limpio, ',')
                ? limpio.Replace(',', '.')
                : limpio.Replace(",", string.Empty);
        }
        else if (puntos > 0)
        {
            // Solo puntos: un solo punto con pinta de miles ("1.500") => quitar; si no, decimal.
            if (puntos > 1 || EsAgrupacionMiles(limpio, '.'))
                limpio = limpio.Replace(".", string.Empty);
        }

        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
            return null;
        return negativo ? -v : v;
    }

    /// <summary>Solo positivos y &gt; 0 (los importes de flete/piso nunca son 0 o negativos).</summary>
    public static decimal? ParsePositivo(string? raw)
    {
        var v = Parse(raw);
        return v is > 0 ? v : null;
    }

    /// <summary>
    /// Un único separador con exactamente 3 dígitos a la derecha y 1–3 a la izquierda
    /// tiene forma de agrupación de miles ("1.500", "12,345"); "1500000,50" o "1234.56" no.
    /// </summary>
    private static bool EsAgrupacionMiles(string s, char separador)
    {
        var pos = s.IndexOf(separador);
        if (pos < 0 || s.IndexOf(separador, pos + 1) >= 0) return false; // más de uno
        var antes = pos;
        var despues = s.Length - pos - 1;
        return despues == 3 && antes >= 1 && antes <= 3;
    }
}
