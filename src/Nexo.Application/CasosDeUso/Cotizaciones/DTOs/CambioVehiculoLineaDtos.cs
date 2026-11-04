using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

/// <summary>
/// Resultado de cambiar el vehículo de una línea. <paramref name="CambioVehiculo"/> false = ya tenía ese vehículo; <paramref name="FleteEliminado"/>
/// true = su flete se calculó con el vehículo anterior y se descartó; <paramref name="Propuesta"/> es la recalculada con el nuevo, o null.
/// </summary>
public sealed record clsLineaVehiculoDto(
    Guid LineaSolicitudId,
    Guid ConfigVehiculoId,
    string TipoVehiculo,
    string? Carroceria,
    bool EsCatalogoOficial,
    bool CambioVehiculo,
    bool FleteEliminado,
    string EstadoLinea,
    string? MotivoPendiente,
    clsPropuestaLineaDto? Propuesta);
