using Nexo.Application.CasosDeUso.RangosPeso.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.RangosPeso;

public static class clsDependencyInjection
{
    public static IServiceCollection AddRangosPesoUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsListarRangosPesoQuery, IReadOnlyList<clsRangoPesoDto>>, clsListarRangosPesoQueryHandler>();
        services.AddScoped<ICommandHandler<clsCrearRangoPesoCommand, clsRangoPesoDto>, clsCrearRangoPesoCommandHandler>();
        services.AddScoped<ICommandHandler<clsEditarRangoPesoCommand, clsRangoPesoDto>, clsEditarRangoPesoCommandHandler>();
        services.AddScoped<ICommandHandler<clsEliminarRangoPesoCommand, bool>, clsEliminarRangoPesoCommandHandler>();
        return services;
    }
}
