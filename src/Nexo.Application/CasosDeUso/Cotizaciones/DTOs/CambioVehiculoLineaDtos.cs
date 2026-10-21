using Nexo.Application.Common.Dtos;

namespace Nexo.Application.CasosDeUso.Cotizaciones.DTOs;

/// <summary>
/// S6-HU-27.3/#322: resultado de cambiar el vehículo de una línea. <paramref name="CambioVehiculo"/> false = la línea ya tenía ese vehículo
/// (no se tocó nada). <paramref name="FleteEliminado"/> true = la línea tenía un flete calculado con el vehículo anterior y se descartó:
/// hay que volver a costearla. <paramref name="Propuesta"/> es la propuesta recalculada de esa línea con el vehículo nuevo (con su piso
/// SICE-TAC); null si el cálculo no devolvió nada para ella.
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
