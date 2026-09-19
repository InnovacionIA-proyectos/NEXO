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
/// siguen viviendo donde están: son un contrato hacia el AGREGADO Solicitud, no
/// hacia este módulo, y ya tienen su propia implementación EF — no se tocan.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSolicitudesUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CrearSolicitudCommand, Guid>, CrearSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<CargarLineasBulkCommand, Guid>, CargarLineasBulkCommandHandler>();
        services.AddScoped<ICommandHandler<RechazarSolicitudCommand, Unit>, RechazarSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<RecalcularScoreCommand, DesgloseScoreDto>, RecalcularScoreCommandHandler>();
        services.AddScoped<ICommandHandler<CapturarSolicitudCommand, CapturaResultDto>, CapturarSolicitudCommandHandler>();
        services.AddScoped<ICommandHandler<ResolverCatalogosDeLineasCommand, IReadOnlyList<CatalogoLineaResueltoDto>>, ResolverCatalogosDeLineasCommandHandler>();

        services.AddScoped<IQueryHandler<ObtenerBandejaQuery, PaginaDto<BandejaItemDto>>, ObtenerBandejaQueryHandler>();
        services.AddScoped<IQueryHandler<ObtenerCosteoQuery, SolicitudCosteoDto>, ObtenerCosteoQueryHandler>();
        services.AddScoped<IQueryHandler<AnalizarExcelQuery, AnalisisExcelDto>, AnalizarExcelQueryHandler>();
        services.AddScoped<IQueryHandler<BuscarClientesQuery, IReadOnlyList<DTOs.ClienteBusquedaDto>>, BuscarClientesQueryHandler>();
        return services;
    }
}
