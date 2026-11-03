using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

/// <summary>S5-HU-21.1/#287: <c>Desde</c>/<c>Hasta</c> opcionales (el mismo filtro que "KPIs del
/// Piloto"). Sin ellos, el comportamiento es idéntico al de antes.</summary>
public sealed record clsObtenerCumplimientoQuery(DateTimeOffset? Desde = null, DateTimeOffset? Hasta = null)
    : IQuery<clsCumplimientoDto>;

public sealed class clsObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<clsObtenerCumplimientoQuery, clsCumplimientoDto>
{
    public Task<clsCumplimientoDto> Handle(clsObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(query.Desde, query.Hasta, ct);
}
