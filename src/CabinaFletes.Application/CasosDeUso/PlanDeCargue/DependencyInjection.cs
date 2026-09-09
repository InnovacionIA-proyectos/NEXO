using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

public static class DependencyInjection
{
    public static IServiceCollection AddPlanDeCargueUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<SolicitarOptimizacionCommand, SolicitudDeCubicacionAceptadaDto>, SolicitarOptimizacionCommandHandler>()
        .AddScoped<IQueryHandler<ObtenerPlanCargueQuery, PlanCargueDto>, ObtenerPlanCargueQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerCubicacionLineaQuery, LineaParaCubicacionDto>, ObtenerCubicacionLineaQueryHandler>()
        // S3-HU-02 (issue #104): los 8 comandos nuevos, todos por el mismo camino de cola.
        .AddScoped<ICommandHandler<CrearPlanCommand, SolicitudDeCubicacionAceptadaDto>, CrearPlanCommandHandler>()
        .AddScoped<ICommandHandler<CrearPlanDemoCommand, ComandoAceptadoDto>, CrearPlanDemoCommandHandler>()
        .AddScoped<ICommandHandler<GuardarVehiculoCommand, ComandoAceptadoDto>, GuardarVehiculoCommandHandler>()
        .AddScoped<ICommandHandler<GuardarItemCommand, ComandoAceptadoDto>, GuardarItemCommandHandler>()
        .AddScoped<ICommandHandler<ImportarItemsCommand, ComandoAceptadoDto>, ImportarItemsCommandHandler>()
        .AddScoped<ICommandHandler<EliminarItemCommand, ComandoAceptadoDto>, EliminarItemCommandHandler>()
        .AddScoped<ICommandHandler<EliminarPlanCommand, ComandoAceptadoDto>, EliminarPlanCommandHandler>()
        .AddScoped<ICommandHandler<AjustarColocacionCommand, ComandoAceptadoDto>, AjustarColocacionCommandHandler>();
}
