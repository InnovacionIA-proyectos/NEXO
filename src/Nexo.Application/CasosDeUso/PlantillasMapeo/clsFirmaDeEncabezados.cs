using System.Security.Cryptography;
using System.Text;
using Nexo.Application.CasosDeUso.Solicitudes;

namespace Nexo.Application.CasosDeUso.PlantillasMapeo;

/// <summary>
/// El resumen con el que se reconoce que un archivo trae los mismos encabezados que una plantilla guardada. Es el SHA-256 de los encabezados
/// normalizados (sin tildes ni mayúsculas, espacios y puntuación colapsados) y EN ORDEN, con su cantidad: «Peso (ton)» y «PESO (TON)» dan la
/// misma firma, pero cambiar una palabra, agregar, quitar o reordenar una columna da otra, y el archivo se analiza como uno nuevo.
/// </summary>
public static class clsFirmaDeEncabezados
{
    public static string Calcular(IReadOnlyList<string?> encabezados)
    {
        var texto = string.Join('\u001f', encabezados.Select(e => clsResolucionDeCatalogos.Normalizar(e))) + $"\u001e{encabezados.Count}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
    }
}
