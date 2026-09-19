using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

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
