using CabinaFletes.Application.CasosDeUso.Metricas.DTOs;
using CabinaFletes.Application.CasosDeUso.Metricas.Interfaces;
using CabinaFletes.Application.Common.Abstractions;

namespace CabinaFletes.Application.CasosDeUso.Metricas;

public sealed record ObtenerCumplimientoQuery : IQuery<CumplimientoDto>;

public sealed class ObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>
{
    public Task<CumplimientoDto> Handle(ObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(ct);
}
