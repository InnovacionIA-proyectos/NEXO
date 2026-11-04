using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>Forma de <see cref="clsAgregarLineaCotizacionCommand"/>; las precondiciones que consultan el repositorio viven en el handler.</summary>
public static class clsAgregarLineaCotizacionValidador
{
    public static void Validar(clsAgregarLineaCotizacionCommand command)
    {
        if (!Enum.TryParse<FuenteFlete>(command.Fuente, ignoreCase: true, out var f) || !Enum.IsDefined(f))
            throw new clsValidationFailedException("FUENTE_INVALIDA", $"Fuente de flete inválida: '{command.Fuente}'.");
    }
}
