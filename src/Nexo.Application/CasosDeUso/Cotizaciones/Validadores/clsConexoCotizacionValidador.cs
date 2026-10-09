using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>
/// Reglas compartidas por agregar/editar un conexo de cotización (S5-HU-18/#262).
/// </summary>
public static class clsConexoCotizacionValidador
{
    /// <summary>tbl_cotizacion_conexos.Cantidad es decimal(12,2) — más de 2 decimales se
    /// truncaría en silencio en la base. El rango (&gt; 0, tope razonable) ya lo valida
    /// [Range] en el request de Api; esto es lo único que faltaba.</summary>
    public static void ValidarCantidad(decimal cantidad)
    {
        if (decimal.Round(cantidad, 2) != cantidad)
            throw new clsValidationFailedException("CANTIDAD_INVALIDA", "La cantidad admite máximo 2 decimales.");
    }

    /// <summary>"individual" (default) o "general" — cualquier otro valor es un error del
    /// cliente, no algo que caiga en silencio a un default.</summary>
    public static string NormalizarOrigen(string? origen)
    {
        var normalizado = (origen ?? "individual").Trim().ToLowerInvariant();
        if (normalizado is not ("individual" or "general"))
            throw new clsValidationFailedException("ORIGEN_INVALIDO", "Origen debe ser 'individual' o 'general'.");
        return normalizado;
    }
}
