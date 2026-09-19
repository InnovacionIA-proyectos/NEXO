using System.Globalization;
using System.Text;

namespace Nexo.Domain.Common;

/// <summary>
/// Normalización de texto libre para cruzar catálogos (rutas, ciudades) provenientes
/// de fuentes distintas (Excel del cliente, export SICE-TAC, captura manual). Centraliza
/// el criterio para que la captura y la importación resuelvan la MISMA ruta y no se
/// dupliquen registros por tildes o mayúsculas ("Bogotá" vs "BOGOTA").
/// </summary>
public static class clsTexto
{
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

    /// <summary>
    /// Clave de ruta + tipo de vehículo (origen|destino|tipoVehiculo) normalizada.
    /// La regla "Solo SICE-TAC" se DEFINE con estas 3 llaves (ver clsReglaSoloSicetac),
    /// pero se venía APLICANDO solo con las 2 primeras (<see cref="ClaveRuta"/>): marcar
    /// un único tipo de vehículo apagaba la comparación contra plaza para TODOS los tipos
    /// de vehículo de la ruta.
    /// </summary>
    public static string ClaveRutaVehiculo(string? origen, string? destino, string? tipoVehiculo) =>
        $"{ClaveRuta(origen, destino)}|{Normalizar(tipoVehiculo)}";

    /// <summary>
    /// Intenta partir "ORIGEN - DESTINO" (formato de NOMBRERUTA del export SICE-TAC) en sus
    /// dos ciudades. Devuelve null si no hay un separador claro.
    /// </summary>
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
