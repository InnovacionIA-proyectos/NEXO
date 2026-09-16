using CabinaFletes.Application.CasosDeUso.Metricas.DTOs;
using CabinaFletes.Application.CasosDeUso.Metricas.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Metricas;

public sealed record ObtenerKpisPilotoQuery(DateTimeOffset? Desde, DateTimeOffset? Hasta) : IQuery<KpisPilotoDto>;

public sealed class ObtenerKpisPilotoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<ObtenerKpisPilotoQuery, KpisPilotoDto>
{
    public Task<KpisPilotoDto> Handle(ObtenerKpisPilotoQuery query, CancellationToken ct = default) =>
        metricas.KpisPilotoAsync(query.Desde, query.Hasta, ct);
}
