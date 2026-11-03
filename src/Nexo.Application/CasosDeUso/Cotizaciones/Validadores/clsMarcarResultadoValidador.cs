using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>
/// Forma de <see cref="clsMarcarResultadoCommand"/>. Defensiva a propósito: aunque Api ya
/// valide el valor por HTTP, un ResultadoCotizacion fuera de rango nunca debe llegar a
/// persistirse. Que la cotización exista o tenga líneas son precondiciones del handler.
/// </summary>
public static class clsMarcarResultadoValidador
{
    public static void Validar(clsMarcarResultadoCommand command)
    {
        if (!Enum.TryParse<ResultadoCotizacion>(command.Resultado, ignoreCase: true, out var resultado)
            || !Enum.IsDefined(resultado)
            || resultado == ResultadoCotizacion.SinDefinir)
            throw new clsValidationFailedException("RESULTADO_INVALIDO", "El resultado debe ser 'ganada' o 'perdida'.");
    }
}
