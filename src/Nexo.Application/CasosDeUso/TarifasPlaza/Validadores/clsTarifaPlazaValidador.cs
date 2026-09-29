using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza.Validadores;

/// <summary>Forma de una tarifa de plaza. Que línea de negocio/clase de viaje existan en el
/// catálogo real, y que la combinación de 7 dimensiones no esté repetida, son precondiciones
/// del caso de uso — necesitan consultar catálogos/repositorio.</summary>
public static class clsTarifaPlazaValidador
{
    public static void Validar(
        string lineaNegocio, string claseViaje, string? claseContenedor,
        int? pesoId, string? tipoVehiculo, decimal valor, string detallesTarifa)
    {
        if (string.IsNullOrWhiteSpace(lineaNegocio) || string.IsNullOrWhiteSpace(claseViaje))
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA", "Línea de negocio y clase de viaje son obligatorias.");
        if (string.IsNullOrWhiteSpace(detallesTarifa))
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA", "Los detalles de la tarifa son obligatorios.");
        if (valor <= 0)
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA", "El valor debe ser mayor que cero.");

        // Verificado contra datos reales (S4-HU-10.1): las tarifas de contenedor usan
        // PesoId Y TipoVehiculo a la vez (el tipo de vehículo se deriva de la banda de
        // peso); las nacionales usan solo TipoVehiculo. La única combinación inválida es
        // que falten las dos — nunca son mutuamente excluyentes.
        if (pesoId is null && string.IsNullOrWhiteSpace(tipoVehiculo))
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA",
                "Indica al menos rango de peso o tipo de vehículo.");
    }
}
