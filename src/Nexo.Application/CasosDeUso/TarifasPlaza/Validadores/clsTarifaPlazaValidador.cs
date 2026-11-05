using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.TarifasPlaza.Validadores;

/// <summary>Forma de una tarifa de plaza. Que línea de negocio/clase de viaje existan en el
/// catálogo real, y que la combinación de 7 dimensiones no esté repetida, son precondiciones
/// del caso de uso — necesitan consultar catálogos/repositorio.</summary>
public static class clsTarifaPlazaValidador
{
    /// <summary>Largo de la columna DetallesTarifa (nvarchar(2000)).</summary>
    public const int MaxDetalles = 2000;

    public static void Validar(
        string lineaNegocio, string claseViaje, string? claseContenedor,
        int? pesoId, string? tipoVehiculo, decimal valor, string? detallesTarifa)
    {
        if (string.IsNullOrWhiteSpace(lineaNegocio) || string.IsNullOrWhiteSpace(claseViaje))
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA", "Línea de negocio y clase de viaje son obligatorias.");
        // Los detalles son OPCIONALES por el API (la Mesa de Costeo no tiene detalle que escribir); la columna es NOT NULL, así que
        // vacío se guarda como texto vacío. La carga por Excel sigue pidiéndolos.
        if (detallesTarifa is { Length: > MaxDetalles })
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA",
                $"Los detalles de la tarifa admiten máximo {MaxDetalles} caracteres (llegaron {detallesTarifa.Length}).");
        if (valor <= 0)
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA", "El valor debe ser mayor que cero.");

        // Las tarifas de contenedor usan PesoId Y TipoVehiculo a la vez; las nacionales solo TipoVehiculo. Lo único inválido es que falten las dos.
        if (pesoId is null && string.IsNullOrWhiteSpace(tipoVehiculo))
            throw new clsValidationFailedException("TARIFA_PLAZA_INVALIDA",
                "Indica al menos rango de peso o tipo de vehículo.");
    }
}
