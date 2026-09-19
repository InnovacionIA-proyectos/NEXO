using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/flota — los vehículos
/// (planes) de la misma flota, contra la proyección local (mismo criterio que
/// `flota()` de Cubicador, ver cubicador/CONTRATO.md §3.2).</summary>
public sealed record clsObtenerFlotaPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<clsPlanFlotaItemDto>>;

public sealed class clsObtenerFlotaPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsObtenerFlotaPlanQuery, IReadOnlyList<clsPlanFlotaItemDto>>
{
    public async Task<IReadOnlyList<clsPlanFlotaItemDto>> Handle(clsObtenerFlotaPlanQuery query, CancellationToken ct = default)
    {
        var hermanos = await planes.ObtenerFlotaAsync(query.PlanCargueId, ct);
        var total = hermanos.Count;

        return hermanos.Select(p => new clsPlanFlotaItemDto(
            p.Id, p.NumeroVehiculo, total,
            UnidadesCargadas: p.Colocaciones.Count(c => c.Cargada),
            UnidadesTotales: p.Colocaciones.Count)).ToList();
    }
}
