using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Cubicacion;
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
        .AddScoped<ICommandHandler<AjustarColocacionCommand, ComandoAceptadoDto>, AjustarColocacionCommandHandler>()
        // Cierra el par de guardar_vehiculo: es la ultima escritura que el navegador
        // hacia directo contra Cubicador, y sin ella S3-HU-05 no puede cerrar.
        .AddScoped<ICommandHandler<EliminarVehiculoCommand, ComandoAceptadoDto>, EliminarVehiculoCommandHandler>()
        // S3-HU-03 (issue #112): lectura del visor 3D, toda contra la proyección local.
        .AddScoped<IQueryHandler<ListarVehiculosQuery, IReadOnlyList<VehiculoCubicacionDto>>, ListarVehiculosQueryHandler>()
        .AddScoped<IQueryHandler<ListarPlanesCargueQuery, IReadOnlyList<PlanCargueDto>>, ListarPlanesCargueQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerResultadoPlanQuery, ResultadoPlanDto>, ObtenerResultadoPlanQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerFlotaPlanQuery, IReadOnlyList<PlanFlotaItemDto>>, ObtenerFlotaPlanQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerMetricasPlanQuery, MetricasCubicacionEvento?>, ObtenerMetricasPlanQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerNoCargadasPlanQuery, IReadOnlyList<ColocacionCargueDto>>, ObtenerNoCargadasPlanQueryHandler>();
}
