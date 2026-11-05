using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>Reglas compartidas por agregar y editar un conexo de cotización.</summary>
public static class clsConexoCotizacionValidador
{
    /// <summary>La cantidad es decimal(12,2): más de 2 decimales se truncaría en silencio en la base. El rango ya lo valida [Range] en el request.</summary>
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
