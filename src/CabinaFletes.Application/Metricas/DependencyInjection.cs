using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Metricas.Dtos;
using CabinaFletes.Application.Metricas.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.Metricas;

public static class DependencyInjection
{
    public static IServiceCollection AddMetricasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto>, ObtenerCumplimientoQueryHandler>();
}
