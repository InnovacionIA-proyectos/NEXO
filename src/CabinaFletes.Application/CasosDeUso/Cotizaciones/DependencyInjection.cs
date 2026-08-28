using CabinaFletes.Application.CasosDeUso.Cotizaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
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
        // La notificación de la entrega es su propio caso de uso: la entrega publica el
        // evento y esto lo atiende, llamado desde el consumidor. Se registra aquí y no en
        // un módulo aparte porque hoy solo la usa este flujo.
        .AddScoped<ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto>, NotificarEntregaCommandHandler>()
        .AddScoped<ICommandHandler<MarcarResultadoCommand, CotizacionDto>, MarcarResultadoCommandHandler>()
        .AddScoped<IQueryHandler<ObtenerCotizacionQuery, CotizacionDto>, ObtenerCotizacionQueryHandler>()
        .AddScoped<IQueryHandler<ExportarCotizacionExcelQuery, ArchivoDto>, ExportarCotizacionExcelQueryHandler>();
}
