using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/resultado — detalle completo
/// (ítems, colocaciones con geometría, métricas, incidencias) contra la proyección
/// local. También sirve GET .../exportar: mismo shape, mismo criterio que usa
/// Cubicador en su propia API síncrona (su endpoint /exportar es, hoy, idéntico a
/// /resultado — ver cubicador/api/routers/planes_cargue.py).
/// </summary>
public sealed record clsObtenerResultadoPlanQuery(Guid PlanCargueId) : IQuery<clsResultadoPlanDto>;

public sealed class clsObtenerResultadoPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<clsObtenerResultadoPlanQuery, clsResultadoPlanDto>
{
    public async Task<clsResultadoPlanDto> Handle(clsObtenerResultadoPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return clsResultadoPlanMapper.ADto(plan);
    }
}
