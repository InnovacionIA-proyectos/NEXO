using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Metricas;

public static class clsDependencyInjection
{
    public static IServiceCollection AddMetricasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsObtenerCumplimientoQuery, clsCumplimientoDto>, clsObtenerCumplimientoQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerKpisPilotoQuery, clsKpisPilotoDto>, clsObtenerKpisPilotoQueryHandler>();
}
