using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Cubicacion;

public static class clsDependencyInjection
{
    public static IServiceCollection AddCubicacionUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsCubicarRapidoQuery, clsCubicacionRapidaResultado>, clsCubicarRapidoQueryHandler>();
}
