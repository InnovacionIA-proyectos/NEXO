using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// GET /api/planes-cargue/{id}/colocaciones — todas las colocaciones del plan (cargadas y no) desde la proyección local; subconjunto de /resultado
/// que, a diferencia de <see cref="clsObtenerNoCargadasPlanQuery"/>, NO filtra por <c>Cargada</c>.
/// </summary>
public sealed record clsObtenerColocacionesPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<clsColocacionCargueDto>>;

public sealed class clsObtenerColocacionesPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>>
{
    public async Task<IReadOnlyList<clsColocacionCargueDto>> Handle(clsObtenerColocacionesPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return plan.Colocaciones.Select(clsResultadoPlanMapper.AColocacion).ToList();
    }
}
