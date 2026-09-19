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
        // Los 8 comandos nuevos, todos por el mismo camino de cola.
        .AddScoped<ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto>, clsCrearPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsCrearPlanDemoCommand, clsComandoAceptadoDto>, clsCrearPlanDemoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarVehiculoCommand, clsComandoAceptadoDto>, clsGuardarVehiculoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarItemCommand, clsComandoAceptadoDto>, clsGuardarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsImportarItemsCommand, clsComandoAceptadoDto>, clsImportarItemsCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarItemCommand, clsComandoAceptadoDto>, clsEliminarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarPlanCommand, clsComandoAceptadoDto>, clsEliminarPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsAjustarColocacionCommand, clsComandoAceptadoDto>, clsAjustarColocacionCommandHandler>()
        // Cierra el par de guardar_vehiculo: es la última escritura que el navegador hacía
        // directo contra Cubicador; sin ella no se puede retirar esa llamada directa.
        .AddScoped<ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto>, clsEliminarVehiculoCommandHandler>()
        // Lectura del visor 3D, toda contra la proyección local.
        .AddScoped<IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>>, clsListarVehiculosQueryHandler>()
        .AddScoped<IQueryHandler<clsListarPlanesCargueQuery, clsPaginaDto<clsPlanCargueDto>>, clsListarPlanesCargueQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerResultadoPlanQuery, clsResultadoPlanDto>, clsObtenerResultadoPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerFlotaPlanQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerFlotaPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerMetricasPlanQuery, clsMetricasCubicacionEvento?>, clsObtenerMetricasPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerNoCargadasPlanQueryHandler>()
        // Los 3 equivalentes que le faltaban a NEXO para que el frontend pudiera dejar de
        // hablarle directo al Cubicador: crear_desde_linea, su sondeo por línea, y colocaciones.
        .AddScoped<ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto>, clsCrearPlanDesdeLineaCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlanesPorLineaQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerPlanesPorLineaQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerColocacionesPlanQueryHandler>();
}
