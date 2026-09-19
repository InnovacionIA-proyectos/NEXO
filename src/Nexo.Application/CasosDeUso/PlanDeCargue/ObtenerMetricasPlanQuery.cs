using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.PlanDeCargue.Mappers;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/metricas — subconjunto de
/// /resultado. <c>null</c> si el plan existe pero nunca se optimizó (mismo criterio que
/// Cubicador: sin colocaciones no hay métricas que calcular).</summary>
public sealed record ObtenerMetricasPlanQuery(Guid PlanCargueId) : IQuery<MetricasCubicacionEvento?>;

public sealed class ObtenerMetricasPlanQueryHandler(IPlanCargueRepository planes)
    : IQueryHandler<ObtenerMetricasPlanQuery, MetricasCubicacionEvento?>
{
    public async Task<MetricasCubicacionEvento?> Handle(ObtenerMetricasPlanQuery query, CancellationToken ct = default)
    {
        var plan = await planes.ObtenerAsync(query.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        return ResultadoPlanMapper.ADto(plan).Metricas;
    }
}
