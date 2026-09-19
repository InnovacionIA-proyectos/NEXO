using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Validadores;

/// <summary>
/// Forma de <see cref="CrearReglaSoloSicetacCommand"/>. Valida sobre el texto ya
/// normalizado: un origen que sea solo espacios no es un origen. Que la regla esté
/// repetida es precondición del caso de uso, necesita consultar el repositorio.
/// </summary>
public static class CrearReglaSoloSicetacValidador
{
    public static void Validar(CrearReglaSoloSicetacCommand command)
    {
        if (ReglaSoloSicetacTexto.Normalizar(command.Origen).Length == 0
            || ReglaSoloSicetacTexto.Normalizar(command.Destino).Length == 0
            || ReglaSoloSicetacTexto.Normalizar(command.TipoVehiculo).Length == 0)
            throw new ValidationFailedException("REGLA_INVALIDA", "Origen, destino y tipo de vehículo son obligatorios.");
    }
}
