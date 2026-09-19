using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>
/// Forma de <see cref="MarcarResultadoCommand"/>. Defensiva a propósito: aunque Api ya
/// valide el valor por HTTP, un ResultadoCotizacion fuera de rango nunca debe llegar a
/// persistirse. Que la cotización exista o tenga líneas son precondiciones del handler.
/// </summary>
public static class MarcarResultadoValidador
{
    public static void Validar(MarcarResultadoCommand command)
    {
        if (!Enum.TryParse<ResultadoCotizacion>(command.Resultado, ignoreCase: true, out var resultado)
            || !Enum.IsDefined(resultado)
            || resultado == ResultadoCotizacion.SinDefinir)
            throw new ValidationFailedException("RESULTADO_INVALIDO", "El resultado debe ser 'ganada' o 'perdida'.");
    }
}
