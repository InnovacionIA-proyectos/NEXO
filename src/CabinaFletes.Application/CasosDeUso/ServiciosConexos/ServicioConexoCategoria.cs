using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos;

/// <summary>
/// Categoría del catálogo de servicios conexos: "logistico" o "adicional".
/// </summary>
public static class ServicioConexoCategoria
{
    /// <summary>Normalización tolerante (para import masivo): nunca falla, cae a "logistico".</summary>
    public static string Normalizada(string? categoria) =>
        (categoria ?? "").Trim().ToUpperInvariant().Contains("ADICION") ? "adicional" : "logistico";

    /// <summary>
    /// Validación estricta (para crear/editar manual): por defecto "logistico"; un valor
    /// NO reconocido se RECHAZA (QA-DATA-007), para que un typo no caiga silenciosamente
    /// a logístico y descuadre los subtotales.
    /// </summary>
    public static string Validada(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return "logistico";
        var raw = categoria.Trim().ToUpperInvariant();
        if (raw.Contains("ADICION")) return "adicional";
        if (raw.Contains("LOGIST")) return "logistico";
        throw new ValidationFailedException("CATEGORIA_INVALIDA", "La categoría debe ser 'logistico' o 'adicional'.");
    }
}
