using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Metricas;

public static class DependencyInjection
{
    public static IServiceCollection AddMetricasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>, ObtenerCumplimientoQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerKpisPilotoQuery, KpisPilotoDto>, ObtenerKpisPilotoQueryHandler>();
}
