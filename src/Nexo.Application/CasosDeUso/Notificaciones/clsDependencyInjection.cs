using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>Correo interno disparado por eventos de otros módulos (Cotizaciones, Biblioteca y Costeo), por eso vive aparte.</summary>
public static class clsDependencyInjection
{
    public static IServiceCollection AddNotificacionesUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsNotificarEntregaCommand, clsResultadoDeNotificacionDto>, clsNotificarEntregaCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto>, clsNotificarConexosCambiadosCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarTarifaVencidaCommand, clsResultadoDeNotificacionDto>, clsNotificarTarifaVencidaCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarLineaFalloTecnicoCommand, clsResultadoDeNotificacionDto>, clsNotificarLineaFalloTecnicoCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarCotizacionGanadaCommand, clsResultadoDeNotificacionDto>, clsNotificarCotizacionGanadaCommandHandler>();
}
