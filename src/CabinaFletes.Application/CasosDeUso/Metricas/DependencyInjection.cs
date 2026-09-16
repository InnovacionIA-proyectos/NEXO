using CabinaFletes.Application.CasosDeUso.Metricas.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Metricas;

public static class DependencyInjection
{
    public static IServiceCollection AddMetricasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>, ObtenerCumplimientoQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerKpisPilotoQuery, KpisPilotoDto>, ObtenerKpisPilotoQueryHandler>();
}
