using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

public sealed record ObtenerKpisPilotoQuery(DateTimeOffset? Desde, DateTimeOffset? Hasta) : IQuery<KpisPilotoDto>;

public sealed class ObtenerKpisPilotoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<ObtenerKpisPilotoQuery, KpisPilotoDto>
{
    public Task<KpisPilotoDto> Handle(ObtenerKpisPilotoQuery query, CancellationToken ct = default) =>
        metricas.KpisPilotoAsync(query.Desde, query.Hasta, ct);
}
