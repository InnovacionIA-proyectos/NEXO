using CabinaFletes.Domain.Common;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Normalización de texto compartida por los Commands de este módulo.</summary>
internal static class ReglaSoloSicetacTexto
{
    /// <summary>
    /// Sin espacios sobrantes, sin tildes, en mayúsculas — para que la coincidencia sea
    /// estable y "Bogotá" y "bogota" no produzcan dos reglas duplicadas para la misma ruta.
    /// </summary>
    public static string Normalizar(string? s) => Texto.Normalizar(s);
}
