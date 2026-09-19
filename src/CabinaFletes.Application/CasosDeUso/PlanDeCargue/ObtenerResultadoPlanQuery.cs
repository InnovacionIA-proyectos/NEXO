using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Mappers;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/resultado — detalle completo
/// (ítems, colocaciones con geometría, métricas, incidencias) contra la proyección
/// local. También sirve GET .../exportar: mismo shape, mismo criterio que usa
/// Cubicador en su propia API síncrona (su endpoint /exportar es, hoy, idéntico a
/// /resultado — ver cubicador/api/routers/planes_cargue.py).
/// </summary>
public sealed record ObtenerResultadoPlanQuery(Guid PlanCargueId) : IQuery<ResultadoPlanDto>;

public sealed class ObtenerResultadoPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerResultadoPlanQuery, ResultadoPlanDto>
{
    public async Task<ResultadoPlanDto> Handle(ObtenerResultadoPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return ResultadoPlanMapper.ADto(plan);
    }
}
