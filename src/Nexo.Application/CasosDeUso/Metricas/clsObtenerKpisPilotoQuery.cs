using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

/// <param name="Tamano">S5-HU-21.1/#287: "1-10", "11-100" o "+100" (por TotalLineas de la saga);
/// null = todos los pliegos. Solo recorta el tiempo de costeo.</param>
public sealed record clsObtenerKpisPilotoQuery(DateTimeOffset? Desde, DateTimeOffset? Hasta, string? Tamano = null)
    : IQuery<clsKpisPilotoDto>;

public sealed class clsObtenerKpisPilotoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<clsObtenerKpisPilotoQuery, clsKpisPilotoDto>
{
    public Task<clsKpisPilotoDto> Handle(clsObtenerKpisPilotoQuery query, CancellationToken ct = default) =>
        metricas.KpisPilotoAsync(query.Desde, query.Hasta, clsTamanoPliego.Parsear(query.Tamano), ct);
}
