using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Cotizaciones;

public static class DependencyInjection
{
    /// <summary>Registra los casos de uso (Handlers) del módulo Cotizaciones.</summary>
    public static IServiceCollection AddCotizacionesUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CrearCotizacionCommand, CotizacionDto>, CrearCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<AgregarLineaCotizacionCommand, CotizacionDto>, AgregarLineaCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<AgregarConexosCotizacionCommand, CotizacionDto>, AgregarConexosCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<AplicarPropuestasCommand, AplicarPropuestasResultDto>, AplicarPropuestasCommandHandler>()
        .AddScoped<ICommandHandler<EntregarCotizacionCommand, EntregarCotizacionResultDto>, EntregarCotizacionCommandHandler>()
        .AddScoped<ICommandHandler<MarcarResultadoCommand, CotizacionDto>, MarcarResultadoCommandHandler>()
        .AddScoped<IQueryHandler<ObtenerCotizacionQuery, CotizacionDto>, ObtenerCotizacionQueryHandler>()
        .AddScoped<IQueryHandler<ExportarCotizacionExcelQuery, ArchivoDto>, ExportarCotizacionExcelQueryHandler>();
}
