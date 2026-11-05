using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.PlantillasMapeo;

/// <summary>
/// <summary>
/// Firma con la que se reconoce que un archivo trae los mismos encabezados que una plantilla guardada; guardar y buscar usan SIEMPRE este cálculo.
/// Cada encabezado se normaliza (sin invisibles, espacios colapsados, "_" y "-" como espacio, sin tildes, mayúsculas) y se hace SHA-256 junto con la cantidad.
/// Una columna vacía en el medio cuenta (el mapeo es por posición); las vacías del final no.
/// </summary>
public static class clsFirmaDeEncabezados
{
    private static readonly Regex Invisibles = new(@"[\p{Cf}]", RegexOptions.Compiled);
    private static readonly Regex Blancos = new(@"\s+", RegexOptions.Compiled);

    /// <summary>La clave de un encabezado según la regla de arriba (vacía si no trae nada).</summary>
    public static string Clave(string? encabezado)
    {
        var limpio = Blancos.Replace(Invisibles.Replace(encabezado ?? string.Empty, string.Empty), " ");
        return clsResolucionDeCatalogos.Normalizar(limpio);
    }

    /// <summary>Las claves de los encabezados sin las columnas vacías del final.</summary>
    public static IReadOnlyList<string> Claves(IReadOnlyList<string?> encabezados)
    {
        var claves = encabezados.Select(Clave).ToList();
        while (claves.Count > 0 && claves[^1].Length == 0) claves.RemoveAt(claves.Count - 1);
        return claves;
    }

    /// <summary>Cuántas columnas cuentan para reconocer un archivo: las que hay hasta la última con encabezado.</summary>
    public static int Cantidad(IReadOnlyList<string?> encabezados) => Claves(encabezados).Count;

    public static string Calcular(IReadOnlyList<string?> encabezados)
    {
        var claves = Claves(encabezados);
        var texto = string.Join('\u001f', claves) + $"\u001e{claves.Count}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
    }
}
