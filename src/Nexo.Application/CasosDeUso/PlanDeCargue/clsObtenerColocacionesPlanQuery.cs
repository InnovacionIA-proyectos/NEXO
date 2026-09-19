using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// GET /api/planes-cargue/{id}/colocaciones — todas las
/// colocaciones del plan (cargadas y no), contra la proyección local. Subconjunto de
/// /resultado, mismo criterio que ya usan <see cref="clsObtenerMetricasPlanQuery"/> y
/// <see cref="clsObtenerNoCargadasPlanQuery"/> — la diferencia con
/// clsObtenerNoCargadasPlanQuery es que este NO filtra por <c>Cargada</c>.
///
/// Resuelve una duda abierta previamente ("colocaciones() puede ser redundante con
/// resultado() — sin confirmar", ver docs/mensajeria/CUBICACION.md §8.2): se decide
/// implementarlo, no eliminarlo. Sí es
/// estrictamente un subconjunto de <c>resultado().colocaciones</c>, pero existir como
/// endpoint propio le da a <c>colocaciones()</c> (cubicador-api.service.ts) un reemplazo
/// 1:1 — cambiar solo la URL, sin tener que reescribir ese call site para leer el payload
/// más grande de /resultado.
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
