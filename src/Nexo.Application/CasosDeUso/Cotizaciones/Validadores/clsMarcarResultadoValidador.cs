using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Cotizaciones.Validadores;

/// <summary>Forma de <see cref="clsMarcarResultadoCommand"/>. Defensiva: aunque Api valide por HTTP, un ResultadoCotizacion fuera de rango nunca debe persistirse.</summary>
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
