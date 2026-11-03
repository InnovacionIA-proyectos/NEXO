using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.RangosPeso.Validadores;

/// <summary>Forma de un rango de peso: PesoMinimo no negativo; PesoMaximo (si viene) mayor
/// que PesoMinimo. Que la banda esté repetida es precondición del caso de uso, necesita
/// consultar el repositorio.</summary>
public static class clsRangoPesoValidador
{
    public static void Validar(int pesoMinimo, int? pesoMaximo)
    {
        if (pesoMinimo < 0)
            throw new clsValidationFailedException("RANGO_PESO_INVALIDO", "El peso mínimo no puede ser negativo.");
        if (pesoMaximo is { } max && max <= pesoMinimo)
            throw new clsValidationFailedException("RANGO_PESO_INVALIDO", "El peso máximo debe ser mayor que el peso mínimo.");
    }
}
