using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Solicitudes.Entidades;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>
/// Módulo Solicitudes/Captura: Bandeja Priorizada (anti-FIFO), alta de solicitudes
/// (spot y captura con Excel), carga masiva de líneas, costeo por línea y rechazo.
/// Antes solo existía un puerto mínimo hacia este módulo desde Cotizaciones
/// (ISolicitudesPort/ILineasSolicitudPort en Cotizaciones.Ports); ESTE módulo es
/// la migración completa del servicio original. Los dos puertos de Cotizaciones
/// siguen viviendo donde están: son un contrato hacia el AGREGADO clsSolicitud, no
/// hacia este módulo, y ya tienen su propia implementación EF — no se tocan.
/// </summary>
public static class clsDependencyInjection
{
    public static IServiceCollection AddSolicitudesUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<clsCrearSolicitudCommand, Guid>, clsCrearSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<clsCargarLineasBulkCommand, Guid>, clsCargarLineasBulkCommandHandler>();
        services.AddScoped<ICommandHandler<clsRechazarSolicitudCommand, Unit>, clsRechazarSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<clsRecalcularScoreCommand, clsDesgloseScoreDto>, clsRecalcularScoreCommandHandler>();
        services.AddScoped<ICommandHandler<clsCapturarSolicitudCommand, clsCapturaResultDto>, clsCapturarSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<clsResolverCatalogosDeLineasCommand, IReadOnlyList<clsCatalogoLineaResueltoDto>>, clsResolverCatalogosDeLineasCommandHandler>();

        services.AddScoped<IQueryHandler<clsObtenerBandejaQuery, clsPaginaDto<clsBandejaItemDto>>, clsObtenerBandejaQueryHandler>();
        services.AddScoped<IQueryHandler<clsObtenerCosteoQuery, clsSolicitudCosteoDto>, clsObtenerCosteoQueryHandler>();
        services.AddScoped<IQueryHandler<clsAnalizarExcelQuery, clsAnalisisExcelDto>, clsAnalizarExcelQueryHandler>();
        services.AddScoped<IQueryHandler<clsBuscarClientesQuery, IReadOnlyList<DTOs.clsClienteBusquedaDto>>, clsBuscarClientesQueryHandler>();
        return services;
    }
}
