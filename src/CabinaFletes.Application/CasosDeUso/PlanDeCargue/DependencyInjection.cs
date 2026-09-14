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
        .AddScoped<IQueryHandler<ObtenerNoCargadasPlanQuery, IReadOnlyList<ColocacionCargueDto>>, ObtenerNoCargadasPlanQueryHandler>()
        // [S3-HU-04.3, issue #78] los 3 equivalentes que le faltaban a NEXO para que el
        // frontend pudiera dejar de hablarle directo al Cubicador (ver el comentario de
        // bloqueo del issue): crear_desde_linea, su sondeo por línea, y colocaciones.
        .AddScoped<ICommandHandler<CrearPlanDesdeLineaCommand, ComandoAceptadoDto>, CrearPlanDesdeLineaCommandHandler>()
        .AddScoped<IQueryHandler<ObtenerPlanesPorLineaQuery, IReadOnlyList<PlanFlotaItemDto>>, ObtenerPlanesPorLineaQueryHandler>()
        .AddScoped<IQueryHandler<ObtenerColocacionesPlanQuery, IReadOnlyList<ColocacionCargueDto>>, ObtenerColocacionesPlanQueryHandler>();
}
