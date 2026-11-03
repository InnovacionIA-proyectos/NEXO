using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Metricas;

/// <summary>
/// S5-HU-21.1/#287: corte del tiempo de costeo por tamaño del pliego (TotalLineas de la saga).
/// El tiempo NO es proporcional al número de líneas —se procesan en paralelo, un mensaje por
/// línea— así que dividir por línea no sirve; se segmenta por tamaño. Los tramos son los de la
/// HU: 1-10 (coincide con UmbralCosteoDiferido = 10: lo que se costea síncrono), 11-100 y +100.
/// </summary>
public enum TamanoPliego { Hasta10, De11A100, Mas100 }

public static class clsTamanoPliego
{
    /// <summary>
    /// null/vacío = todos. Acepta "1-10", "11-100" y "+100" (también "100+", "mas100",
    /// ">100"): un "+" sin codificar en la URL llega como espacio, así que el espacio se ignora.
    /// Cualquier otro valor es un error del cliente, no algo que caiga en silencio a "todos".
    /// </summary>
    public static TamanoPliego? Parsear(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = new string(texto.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
        return t switch
        {
            "1-10" => TamanoPliego.Hasta10,
            "11-100" => TamanoPliego.De11A100,
            // "100" a secas: es lo que queda de un "+100" sin codificar en la URL (el "+" llega
            // como espacio y se ignora arriba).
            "+100" or "100+" or "100" or "mas100" or "mas-100" or ">100" => TamanoPliego.Mas100,
            _ => throw new clsValidationFailedException("TAMANO_INVALIDO", "tamano debe ser '1-10', '11-100' o '+100'."),
        };
    }

    public static string Etiqueta(TamanoPliego t) => t switch
    {
        TamanoPliego.Hasta10 => "1-10",
        TamanoPliego.De11A100 => "11-100",
        _ => "+100",
    };

    public static bool Contiene(TamanoPliego t, int totalLineas) => t switch
    {
        TamanoPliego.Hasta10 => totalLineas is >= 1 and <= 10,
        TamanoPliego.De11A100 => totalLineas is >= 11 and <= 100,
        _ => totalLineas > 100,
    };
}
