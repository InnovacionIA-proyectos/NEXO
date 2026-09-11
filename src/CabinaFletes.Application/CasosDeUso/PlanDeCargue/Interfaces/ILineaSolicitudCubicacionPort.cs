using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// Puerto de solo lectura, propio y dedicado de Plan de Cargue hacia Solicitudes —
/// deliberadamente NO comparte los puertos de Cotizaciones/Costeo hacia Solicitudes:
/// esos exponen operaciones de escritura/rastreo que este puente no necesita, y esta es
/// justo la porción mínima que sí necesita. Es lo que usa "Crear plan desde línea" (Mesa
/// de Costeo → Cubicador 3D, cubicador/CONTRATO.md sección 7).
/// </summary>
public interface ILineaSolicitudCubicacionPort
{
    /// <summary>Null si la línea no existe.</summary>
    Task<LineaParaCubicacionDto?> ObtenerAsync(Guid lineaSolicitudId, CancellationToken ct = default);
}
