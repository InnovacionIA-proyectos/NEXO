using System.Globalization;

namespace Nexo.Domain.Common;

/// <summary>
/// Parser de importes/pesos que llegan como texto de fuentes heterogéneas. Acepta el formato colombiano
/// ("1.500.000,50") y el invariante ("1,500,000.50"); el separador decimal es el que aparece más a la derecha.
/// </summary>
public static class clsImporteParser
{
    public static decimal? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // Solo dígitos, signo y separadores (quita "$", NBSP, letras).
        var limpio = new string(raw.Where(c => char.IsDigit(c) || c is '.' or ',' or '-' or '+').ToArray());
        if (limpio.Length == 0) return null;

        var negativo = limpio.TrimStart('+').StartsWith('-');
        limpio = limpio.Replace("+", string.Empty).Replace("-", string.Empty);
        if (limpio.Length == 0) return null;

        var puntos = limpio.Count(c => c == '.');
        var comas = limpio.Count(c => c == ',');

        if (puntos > 0 && comas > 0)
        {
            var dec = limpio.LastIndexOf('.') > limpio.LastIndexOf(',') ? '.' : ',';
            var miles = dec == '.' ? ',' : '.';
            limpio = limpio.Replace(miles.ToString(), string.Empty).Replace(dec, '.');
        }
        else if (comas > 0)
        {
            // Una sola coma que no parece de miles es decimal; el resto son miles.
            limpio = comas == 1 && !EsAgrupacionMiles(limpio, ',')
                ? limpio.Replace(',', '.')
                : limpio.Replace(",", string.Empty);
        }
        else if (puntos > 0)
        {
            // Un solo punto con forma de miles ("1.500") se quita; si no, es decimal.
            if (puntos > 1 || EsAgrupacionMiles(limpio, '.'))
                limpio = limpio.Replace(".", string.Empty);
        }

        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v))
            return null;
        return negativo ? -v : v;
    }

    /// <summary>Solo positivos: los importes de flete/piso nunca son 0 ni negativos.</summary>
    public static decimal? ParsePositivo(string? raw)
    {
        var v = Parse(raw);
        return v is > 0 ? v : null;
    }

    /// <summary>Un único separador con 3 dígitos a la derecha y 1–3 a la izquierda es agrupación de miles ("1.500", "12,345").</summary>
    private static bool EsAgrupacionMiles(string s, char separador)
    {
        var pos = s.IndexOf(separador);
        if (pos < 0 || s.IndexOf(separador, pos + 1) >= 0) return false;
        var antes = pos;
        var despues = s.Length - pos - 1;
        return despues == 3 && antes >= 1 && antes <= 3;
    }
}
