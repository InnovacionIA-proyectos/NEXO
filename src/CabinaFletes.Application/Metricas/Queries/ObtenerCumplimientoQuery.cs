using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Metricas.Dtos;
using CabinaFletes.Application.Metricas.Ports;

namespace CabinaFletes.Application.Metricas.Queries;

public sealed record ObtenerCumplimientoQuery : IQuery<CumplimientoDto>;

public sealed class ObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>
{
    public Task<CumplimientoDto> Handle(ObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(ct);
}
