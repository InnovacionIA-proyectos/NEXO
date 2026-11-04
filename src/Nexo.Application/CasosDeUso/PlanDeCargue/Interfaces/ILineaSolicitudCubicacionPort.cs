using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Puerto de solo lectura de Plan de Cargue hacia Solicitudes, deliberadamente propio: expone solo la porción mínima que necesita "Crear plan desde línea".</summary>
public interface ILineaSolicitudCubicacionPort
{
    /// <summary>Null si la línea no existe.</summary>
    Task<clsLineaParaCubicacionDto?> ObtenerAsync(Guid lineaSolicitudId, CancellationToken ct = default);
}
