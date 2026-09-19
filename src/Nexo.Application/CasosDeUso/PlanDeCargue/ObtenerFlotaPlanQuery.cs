using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/flota — los vehículos
/// (planes) de la misma flota, contra la proyección local (mismo criterio que
/// `flota()` de Cubicador, ver cubicador/CONTRATO.md §3.2).</summary>
public sealed record ObtenerFlotaPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<PlanFlotaItemDto>>;

public sealed class ObtenerFlotaPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerFlotaPlanQuery, IReadOnlyList<PlanFlotaItemDto>>
{
    public async Task<IReadOnlyList<PlanFlotaItemDto>> Handle(ObtenerFlotaPlanQuery query, CancellationToken ct = default)
    {
        var hermanos = await planes.ObtenerFlotaAsync(query.PlanCargueId, ct);
        var total = hermanos.Count;

        return hermanos.Select(p => new PlanFlotaItemDto(
            p.Id, p.NumeroVehiculo, total,
            UnidadesCargadas: p.Colocaciones.Count(c => c.Cargada),
            UnidadesTotales: p.Colocaciones.Count)).ToList();
    }
}
