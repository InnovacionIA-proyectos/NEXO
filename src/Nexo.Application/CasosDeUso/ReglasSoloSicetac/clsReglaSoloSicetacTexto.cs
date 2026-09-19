using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Normalización de texto compartida por los Commands de este módulo.</summary>
internal static class clsReglaSoloSicetacTexto
{
    /// <summary>
    /// Sin espacios sobrantes, sin tildes, en mayúsculas — para que la coincidencia sea
    /// estable y "Bogotá" y "bogota" no produzcan dos reglas duplicadas para la misma ruta.
    /// </summary>
    public static string Normalizar(string? s) => clsTexto.Normalizar(s);
}
