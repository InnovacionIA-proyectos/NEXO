using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

public sealed record clsObtenerKpisPilotoQuery(DateTimeOffset? Desde, DateTimeOffset? Hasta) : IQuery<clsKpisPilotoDto>;

public sealed class clsObtenerKpisPilotoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<clsObtenerKpisPilotoQuery, clsKpisPilotoDto>
{
    public Task<clsKpisPilotoDto> Handle(clsObtenerKpisPilotoQuery query, CancellationToken ct = default) =>
        metricas.KpisPilotoAsync(query.Desde, query.Hasta, ct);
}
