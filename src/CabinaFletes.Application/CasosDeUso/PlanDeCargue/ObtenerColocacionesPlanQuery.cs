using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// [S3-HU-04.3, issue #78] GET /api/planes-cargue/{id}/colocaciones — todas las
/// colocaciones del plan (cargadas y no), contra la proyección local. Subconjunto de
/// /resultado, mismo criterio que ya usan <see cref="ObtenerMetricasPlanQuery"/> y
/// <see cref="ObtenerNoCargadasPlanQuery"/> (issue #112, S3-HU-03) — la diferencia con
/// ObtenerNoCargadasPlanQuery es que este NO filtra por <c>Cargada</c>.
///
/// Resuelve la duda que dejó abierta el comentario de bloqueo de este issue
/// ("colocaciones() puede ser redundante con resultado() — sin confirmar", y
/// docs/mensajeria/CUBICACION.md §8.2): se decide implementarlo, no eliminarlo. Sí es
/// estrictamente un subconjunto de <c>resultado().colocaciones</c>, pero existir como
/// endpoint propio le da a <c>colocaciones()</c> (cubicador-api.service.ts) un reemplazo
/// 1:1 — cambiar solo la URL, sin tener que reescribir ese call site para leer el payload
/// más grande de /resultado.
/// </summary>
public sealed record ObtenerColocacionesPlanQuery(Guid PlanCargueId) : IQuery<IReadOnlyList<ColocacionCargueDto>>;

public sealed class ObtenerColocacionesPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerColocacionesPlanQuery, IReadOnlyList<ColocacionCargueDto>>
{
    public async Task<IReadOnlyList<ColocacionCargueDto>> Handle(ObtenerColocacionesPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return plan.Colocaciones.Select(ResultadoPlanMapper.AColocacion).ToList();
    }
}
