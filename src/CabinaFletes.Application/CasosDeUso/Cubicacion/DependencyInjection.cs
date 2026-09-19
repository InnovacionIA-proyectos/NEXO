using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Cubicacion;

public static class DependencyInjection
{
    public static IServiceCollection AddCubicacionUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<CubicarRapidoQuery, CubicacionRapidaResultado>, CubicarRapidoQueryHandler>();
}
