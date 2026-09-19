using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Mensajes.Cubicacion;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

public static class DependencyInjection
{
    public static IServiceCollection AddPlanDeCargueUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsSolicitarOptimizacionCommand, clsSolicitudDeCubicacionAceptadaDto>, clsSolicitarOptimizacionCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlanCargueQuery, clsPlanCargueDto>, clsObtenerPlanCargueQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerCubicacionLineaQuery, clsLineaParaCubicacionDto>, clsObtenerCubicacionLineaQueryHandler>()
        // S3-HU-02 (issue #104): los 8 comandos nuevos, todos por el mismo camino de cola.
        .AddScoped<ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto>, clsCrearPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsCrearPlanDemoCommand, clsComandoAceptadoDto>, clsCrearPlanDemoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarVehiculoCommand, clsComandoAceptadoDto>, clsGuardarVehiculoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarItemCommand, clsComandoAceptadoDto>, clsGuardarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsImportarItemsCommand, clsComandoAceptadoDto>, clsImportarItemsCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarItemCommand, clsComandoAceptadoDto>, clsEliminarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarPlanCommand, clsComandoAceptadoDto>, clsEliminarPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsAjustarColocacionCommand, clsComandoAceptadoDto>, clsAjustarColocacionCommandHandler>()
        // Cierra el par de guardar_vehiculo: es la ultima escritura que el navegador
        // hacia directo contra Cubicador, y sin ella S3-HU-05 no puede cerrar.
        .AddScoped<ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto>, clsEliminarVehiculoCommandHandler>()
        // S3-HU-03 (issue #112): lectura del visor 3D, toda contra la proyección local.
        .AddScoped<IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>>, clsListarVehiculosQueryHandler>()
        .AddScoped<IQueryHandler<clsListarPlanesCargueQuery, clsPaginaDto<clsPlanCargueDto>>, clsListarPlanesCargueQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerResultadoPlanQuery, clsResultadoPlanDto>, clsObtenerResultadoPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerFlotaPlanQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerFlotaPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerMetricasPlanQuery, clsMetricasCubicacionEvento?>, clsObtenerMetricasPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerNoCargadasPlanQueryHandler>()
        // [S3-HU-04.3, issue #78] los 3 equivalentes que le faltaban a NEXO para que el
        // frontend pudiera dejar de hablarle directo al Cubicador (ver el comentario de
        // bloqueo del issue): crear_desde_linea, su sondeo por línea, y colocaciones.
        .AddScoped<ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto>, clsCrearPlanDesdeLineaCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlanesPorLineaQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerPlanesPorLineaQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerColocacionesPlanQueryHandler>();
}
