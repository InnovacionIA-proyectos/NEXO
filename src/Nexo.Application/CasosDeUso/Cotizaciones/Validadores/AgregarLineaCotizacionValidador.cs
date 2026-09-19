using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>
/// Forma de <see cref="AgregarLineaCotizacionCommand"/>. Que la línea pertenezca a la
/// cotización, o que exista una tarifa vigente, son precondiciones del caso de uso:
/// necesitan consultar el repositorio y por eso viven en el handler, no aquí.
/// </summary>
public static class AgregarLineaCotizacionValidador
{
    public static void Validar(AgregarLineaCotizacionCommand command)
    {
        if (!Enum.TryParse<FuenteFlete>(command.Fuente, ignoreCase: true, out var f) || !Enum.IsDefined(f))
            throw new ValidationFailedException("FUENTE_INVALIDA", $"Fuente de flete inválida: '{command.Fuente}'.");
    }
}
