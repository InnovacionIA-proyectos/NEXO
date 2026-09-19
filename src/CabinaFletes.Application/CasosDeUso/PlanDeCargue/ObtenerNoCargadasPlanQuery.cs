using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/no-cargadas — subconjunto
/// de /resultado, solo las colocaciones rechazadas (con su <c>CodigoRechazo</c>).</summary>
public sealed record ObtenerNoCargadasPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<ColocacionCargueDto>>;

public sealed class ObtenerNoCargadasPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerNoCargadasPlanQuery, IReadOnlyList<ColocacionCargueDto>>
{
    public async Task<IReadOnlyList<ColocacionCargueDto>> Handle(ObtenerNoCargadasPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return plan.Colocaciones.Where(c => !c.Cargada).Select(ResultadoPlanMapper.AColocacion).ToList();
    }
}
