using System.Globalization;
using System.Text;

namespace Nexo.Domain.Common;

/// <summary>Normalización de texto libre para cruzar catálogos de fuentes distintas sin duplicar por tildes o mayúsculas ("Bogotá" vs "BOGOTA").</summary>
public static class clsTexto
{
    /// <summary>Pasa un nombre de enumerado (<c>PuertoAInterior</c>) a texto con minúscula inicial (<c>puertoAInterior</c>). Null si no hay valor.</summary>
    public static string? MinusculaInicial<T>(T? valor) where T : struct, Enum =>
        valor is { } v ? MinusculaInicial(v.ToString()) : null;

    public static string? MinusculaInicial(string? s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>Colapsa espacios repetidos y recorta; null si queda vacío.</summary>
    public static string? Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Quita marcas diacríticas (tildes, diéresis) conservando la letra base.</summary>
    public static string SinTildes(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Forma canónica para comparar: sin tildes, sin espacios repetidos, en mayúsculas.</summary>
    public static string Normalizar(string? s) => SinTildes(Limpiar(s) ?? string.Empty).ToUpperInvariant();

    /// <summary>Clave de una ruta (origen|destino) normalizada para reutilizarla entre fuentes.</summary>
    public static string ClaveRuta(string? origen, string? destino) => $"{Normalizar(origen)}|{Normalizar(destino)}";

    /// <summary>Clave normalizada origen|destino|tipoVehiculo; la regla "Solo SICE-TAC" se define con estas 3 llaves.</summary>
    public static string ClaveRutaVehiculo(string? origen, string? destino, string? tipoVehiculo) =>
        $"{ClaveRuta(origen, destino)}|{Normalizar(tipoVehiculo)}";

    /// <summary>Parte "ORIGEN - DESTINO" (formato NOMBRERUTA de SICE-TAC) en sus dos ciudades; null si no hay separador claro.</summary>
    public static (string Origen, string Destino)? PartirRuta(string? nombreRuta)
    {
        var limpio = Limpiar(nombreRuta);
        if (limpio is null) return null;

        foreach (var sep in new[] { " - ", " – ", " — ", "→", "->" })
        {
            var idx = limpio.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx <= 0 || idx + sep.Length >= limpio.Length) continue;
            var o = Limpiar(limpio[..idx]);
            var d = Limpiar(limpio[(idx + sep.Length)..]);
            if (o is not null && d is not null) return (o, d);
        }
        return null;
    }
}
