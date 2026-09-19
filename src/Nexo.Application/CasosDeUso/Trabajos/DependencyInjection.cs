using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Trabajos;

public static class DependencyInjection
{
    public static IServiceCollection AddTrabajosUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<ConsultarTrabajoQuery, EstadoTrabajoDto>, ConsultarTrabajoQueryHandler>();
        return services;
    }
}
