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
        .AddScoped<ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto>, clsAgregarConexosCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEditarConexoCotizacionCommand, clsCotizacionDto>, clsEditarConexoCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarConexoCotizacionCommand, clsCotizacionDto>, clsEliminarConexoCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsAplicarPropuestasCommand, clsAplicarPropuestasResultDto>, clsAplicarPropuestasCommandHandler>()
        .AddScoped<ICommandHandler<clsEntregarCotizacionCommand, clsEntregarCotizacionResultDto>, clsEntregarCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<clsMarcarResultadoCommand, clsCotizacionDto>, clsMarcarResultadoCommandHandler>()
        .AddScoped<IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto>, clsObtenerCotizacionQueryHandler>()
        .AddScoped<IQueryHandler<clsExportarCotizacionExcelQuery, clsArchivoDto>, clsExportarCotizacionExcelQueryHandler>();
}
