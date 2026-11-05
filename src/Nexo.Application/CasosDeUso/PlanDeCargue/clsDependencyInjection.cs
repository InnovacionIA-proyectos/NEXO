using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Mensajes.Cubicacion;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

public static class clsDependencyInjection
{
    public static IServiceCollection AddPlanDeCargueUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsSolicitarOptimizacionCommand, clsSolicitudDeCubicacionAceptadaDto>, clsSolicitarOptimizacionCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlanCargueQuery, clsPlanCargueDto>, clsObtenerPlanCargueQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerCubicacionLineaQuery, clsLineaParaCubicacionDto>, clsObtenerCubicacionLineaQueryHandler>()
        .AddScoped<ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto>, clsCrearPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsCrearPlanDemoCommand, clsComandoAceptadoDto>, clsCrearPlanDemoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarVehiculoCommand, clsComandoAceptadoDto>, clsGuardarVehiculoCommandHandler>()
        .AddScoped<ICommandHandler<clsGuardarItemCommand, clsComandoAceptadoDto>, clsGuardarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsImportarItemsCommand, clsComandoAceptadoDto>, clsImportarItemsCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarItemCommand, clsComandoAceptadoDto>, clsEliminarItemCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarPlanCommand, clsComandoAceptadoDto>, clsEliminarPlanCommandHandler>()
        .AddScoped<ICommandHandler<clsAjustarColocacionCommand, clsComandoAceptadoDto>, clsAjustarColocacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto>, clsEliminarVehiculoCommandHandler>()
        // Lectura del visor 3D, toda contra la proyección local.
        .AddScoped<IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>>, clsListarVehiculosQueryHandler>()
        .AddScoped<IQueryHandler<clsListarPlanesCargueQuery, clsPaginaDto<clsPlanCargueDto>>, clsListarPlanesCargueQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerResultadoPlanQuery, clsResultadoPlanDto>, clsObtenerResultadoPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerFlotaPlanQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerFlotaPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerMetricasPlanQuery, clsMetricasCubicacionEvento?>, clsObtenerMetricasPlanQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerNoCargadasPlanQueryHandler>()
        .AddScoped<ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto>, clsCrearPlanDesdeLineaCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlanesPorLineaQuery, IReadOnlyList<clsPlanFlotaItemDto>>, clsObtenerPlanesPorLineaQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerEstadoCubicacionLineaQuery, clsEstadoCubicacionLineaDto>, clsObtenerEstadoCubicacionLineaQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>>, clsObtenerColocacionesPlanQueryHandler>();
}
