using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

public sealed record ObtenerCumplimientoQuery : IQuery<CumplimientoDto>;

public sealed class ObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>
{
    public Task<CumplimientoDto> Handle(ObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(ct);
}
