using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/no-cargadas — subconjunto
/// de /resultado, solo las colocaciones rechazadas (con su <c>CodigoRechazo</c>).</summary>
public sealed record clsObtenerNoCargadasPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<clsColocacionCargueDto>>;

public sealed class clsObtenerNoCargadasPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>>
{
    public async Task<IReadOnlyList<clsColocacionCargueDto>> Handle(clsObtenerNoCargadasPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return plan.Colocaciones.Where(c => !c.Cargada).Select(clsResultadoPlanMapper.AColocacion).ToList();
    }
}
