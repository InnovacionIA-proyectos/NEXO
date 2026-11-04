using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac.Validadores;

/// <summary>Forma de <see cref="clsCrearReglaSoloSicetacCommand"/> sobre el texto ya normalizado. Que la regla esté repetida es precondición del caso de uso.</summary>
public static class clsCrearReglaSoloSicetacValidador
{
    public static void Validar(clsCrearReglaSoloSicetacCommand command)
    {
        if (clsReglaSoloSicetacTexto.Normalizar(command.Origen).Length == 0
            || clsReglaSoloSicetacTexto.Normalizar(command.Destino).Length == 0
            || clsReglaSoloSicetacTexto.Normalizar(command.TipoVehiculo).Length == 0)
            throw new clsValidationFailedException("REGLA_INVALIDA", "Origen, destino y tipo de vehículo son obligatorios.");
    }
}
