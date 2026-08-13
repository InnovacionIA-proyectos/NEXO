namespace CabinaFletes.Application.ReglasSoloSicetac;

/// <summary>Normalización de texto compartida por los Commands de este módulo.</summary>
internal static class ReglaSoloSicetacTexto
{
    /// <summary>Sin espacios sobrantes, en mayúsculas — para que la coincidencia sea estable.</summary>
    public static string Normalizar(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? string.Empty
            : string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();
}
