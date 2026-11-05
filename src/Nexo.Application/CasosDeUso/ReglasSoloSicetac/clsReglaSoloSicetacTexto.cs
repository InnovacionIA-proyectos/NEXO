using Nexo.Domain.Common;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

/// <summary>Normalización de texto compartida por los Commands de este módulo.</summary>
internal static class clsReglaSoloSicetacTexto
{
    /// <summary>Sin espacios sobrantes, sin tildes y en mayúsculas, para que "Bogotá" y "bogota" no produzcan reglas duplicadas.</summary>
    public static string Normalizar(string? s) => clsTexto.Normalizar(s);
}
