using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Módulo Notificaciones: correo interno disparado por eventos de otros módulos
/// (entrega de cotización, tarifa vencida, línea sin costear, cotización ganada).
/// Vive aparte porque ninguna de estas notificaciones es dueña de un solo módulo —
/// las dispara Cotizaciones, Biblioteca y Costeo por igual.
/// </summary>
public static class clsDependencyInjection
{
    public static IServiceCollection AddNotificacionesUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsNotificarEntregaCommand, clsResultadoDeNotificacionDto>, clsNotificarEntregaCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto>, clsNotificarConexosCambiadosCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarTarifaVencidaCommand, clsResultadoDeNotificacionDto>, clsNotificarTarifaVencidaCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarLineaFalloTecnicoCommand, clsResultadoDeNotificacionDto>, clsNotificarLineaFalloTecnicoCommandHandler>()
        .AddScoped<ICommandHandler<clsNotificarCotizacionGanadaCommand, clsResultadoDeNotificacionDto>, clsNotificarCotizacionGanadaCommandHandler>();
}
