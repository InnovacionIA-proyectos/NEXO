using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

public static class DependencyInjection
{
    public static IServiceCollection AddPlanDeCargueUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<SolicitarOptimizacionCommand, SolicitudDeCubicacionAceptadaDto>, SolicitarOptimizacionCommandHandler>()
        .AddScoped<IQueryHandler<ObtenerPlanCargueQuery, PlanCargueDto>, ObtenerPlanCargueQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerCubicacionLineaQuery, LineaParaCubicacionDto>, ObtenerCubicacionLineaQueryHandler>();
}
