using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// GET /api/planes-cargue/por-linea/{lineaId} — sondeo de la flota que crea <see cref="clsCrearPlanDesdeLineaCommand"/>, desde la proyección local.
/// Responde lista VACÍA (no 404) mientras el comando sigue en proceso: sin consultar a Cubicador no se distingue "no existe" de "aún no llegó", y el
/// frontend la interpreta como "seguir esperando".
/// </summary>
public sealed record clsObtenerPlanesPorLineaQuery(Guid LineaSolicitudId) : IQuery<IReadOnlyList<clsPlanFlotaItemDto>>;

public sealed class clsObtenerPlanesPorLineaQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsObtenerPlanesPorLineaQuery, IReadOnlyList<clsPlanFlotaItemDto>>
{
    public async Task<IReadOnlyList<clsPlanFlotaItemDto>> Handle(clsObtenerPlanesPorLineaQuery query, CancellationToken ct = default)
    {
        var flota = await planes.ObtenerPorLineaAsync(query.LineaSolicitudId, ct);
        var total = flota.Count;

        return flota.Select(p => new clsPlanFlotaItemDto(
            p.Id, p.NumeroVehiculo, total,
            UnidadesCargadas: p.Colocaciones.Count(c => c.Cargada),
            UnidadesTotales: p.Colocaciones.Count)).ToList();
    }
}
