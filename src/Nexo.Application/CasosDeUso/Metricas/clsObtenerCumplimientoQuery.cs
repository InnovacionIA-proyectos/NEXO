using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.CasosDeUso.Metricas.Interfaces;
using Nexo.Application.Common.Abstractions;

namespace Nexo.Application.CasosDeUso.Metricas;

public sealed record clsObtenerCumplimientoQuery : IQuery<clsCumplimientoDto>;

public sealed class clsObtenerCumplimientoQueryHandler(IMetricasPort metricas)
    : IQueryHandler<clsObtenerCumplimientoQuery, clsCumplimientoDto>
{
    public Task<clsCumplimientoDto> Handle(clsObtenerCumplimientoQuery query, CancellationToken ct = default) =>
        metricas.CumplimientoAsync(ct);
}
