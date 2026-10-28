using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.PlantillasMapeo;

/// <summary>
/// El resumen con el que se reconoce que un archivo trae los mismos encabezados que una plantilla guardada. Guardar y buscar usan SIEMPRE este
/// mismo cálculo: si uno normalizara distinto del otro, la plantilla nunca coincidiría y se llamaría a la IA en cada archivo.
///
/// La regla, exacta (cada encabezado, en este orden):
/// <list type="number">
///   <item>Se quitan los caracteres invisibles (ancho cero, marca de orden de bytes).</item>
///   <item>Cualquier espacio en blanco (espacio, tabulador, salto de línea, espacio de no separación) pasa a un espacio, y los repetidos, y los de
///         los extremos, se quitan: «Peso&#160;(kg)», «Peso\n(kg)» y « Peso  (kg) » son el mismo encabezado.</item>
///   <item>Un espacio antes de una coma o un punto se quita («Peso , kg» = «Peso, kg»), y <c>_</c> y <c>-</c> cuentan como espacio.</item>
///   <item>Se quitan las tildes y todo pasa a mayúsculas: «Peso (TON)» = «peso (ton)» = «Péso (Ton)».</item>
/// </list>
/// Lo demás SÍ distingue: otra palabra, otra puntuación («Peso(kg)» no es «Peso (kg)»), otro orden y otra cantidad de columnas.
/// Se hace un SHA-256 de los encabezados así normalizados, unidos por un separador, más la cantidad.
///
/// Columnas vacías: una columna sin encabezado en el medio SÍ cuenta (ocupa su posición, y el mapeo es por posición); las vacías que sobran
/// AL FINAL no, porque Excel suele reportar columnas de más que el usuario nunca vio.
///
/// Qué fila trae los encabezados y qué pasa con las celdas combinadas lo resuelve quien lee el archivo: aquí solo llega la lista de
/// encabezados ya resuelta, una por columna, y la regla de arriba se aplica a lo que llegue. Que dos cargas del mismo formato manden la misma
/// fila de encabezados es responsabilidad del lector del archivo.
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
