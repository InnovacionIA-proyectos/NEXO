using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Cotizaciones;

public static class clsDependencyInjection
{
    /// <summary>Registra los casos de uso (Handlers) del módulo Cotizaciones.</summary>
    public static IServiceCollection AddCotizacionesUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsCrearCotizacionCommand, clsCotizacionDto>, clsCrearCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsAgregarLineaCotizacionCommand, clsCotizacionDto>, clsAgregarLineaCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEditarLineaCotizacionCommand, clsCotizacionDto>, clsEditarLineaCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsRechazarLineaCotizacionCommand, clsLineaRechazoDto>, clsRechazarLineaCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsDeshacerRechazoLineaCotizacionCommand, clsLineaRechazoDto>, clsDeshacerRechazoLineaCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsCambiarVehiculoLineaCommand, clsLineaVehiculoDto>, clsCambiarVehiculoLineaCommandHandler>()
        .AddScoped<ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto>, clsAgregarConexosCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEditarConexoCotizacionCommand, clsCotizacionDto>, clsEditarConexoCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarConexoCotizacionCommand, clsCotizacionDto>, clsEliminarConexoCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsAplicarPropuestasCommand, clsAplicarPropuestasResultDto>, clsAplicarPropuestasCommandHandler>()
        .AddScoped<ICommandHandler<clsEntregarCotizacionCommand, clsEntregarCotizacionResultDto>, clsEntregarCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsMarcarResultadoCommand, clsCotizacionDto>, clsMarcarResultadoCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>, clsObtenerCotizacionQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerCotizacionPorSolicitudQuery, clsCotizacionDto?>, clsObtenerCotizacionPorSolicitudQueryHandler>()
        .AddScoped<IQueryHandler<clsExportarCotizacionExcelQuery, clsArchivoDto>, clsExportarCotizacionExcelQueryHandler>();
}
