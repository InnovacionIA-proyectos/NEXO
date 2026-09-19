using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Trabajos;

public static class DependencyInjection
{
    public static IServiceCollection AddTrabajosUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<ConsultarTrabajoQuery, EstadoTrabajoDto>, ConsultarTrabajoQueryHandler>();
        return services;
    }
}
