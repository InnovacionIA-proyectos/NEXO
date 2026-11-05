using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

/// <summary><c>Desde</c>/<c>Hasta</c> opcionales, el mismo filtro que "KPIs del Piloto".</summary>
public sealed record clsObtenerCumplimientoQuery(DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null)
    : IQuery<clsCumplimientoDto>;

public sealed class clsObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<clsObtenerCumplimientoQuery, clsCumplimientoDto>
{
    public Task<clsCumplimientoDto> Handle(clsObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(query.Desde, query.Hasta, ct);
}
