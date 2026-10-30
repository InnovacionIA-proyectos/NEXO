using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Trabajos;

public static class clsDependencyInjection
{
    public static IServiceCollection AddTrabajosUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsConsultarTrabajoQuery, clsEstadoTrabajoDto>, clsConsultarTrabajoQueryHandler>();
        return services;
    }
}
