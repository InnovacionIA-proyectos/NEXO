using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Notificaciones;

/// <summary>
/// Módulo Notificaciones: correo interno disparado por eventos de otros módulos
/// (entrega de cotización, tarifa vencida, línea sin costear, cotización ganada).
/// Vive aparte porque ninguna de estas notificaciones es dueña de un solo módulo —
/// las dispara Cotizaciones, Biblioteca y Costeo por igual.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNotificacionesUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto>, NotificarEntregaCommandHandler>()
        .AddScoped<ICommandHandler<NotificarTarifaVencidaCommand, ResultadoDeNotificacionDto>, NotificarTarifaVencidaCommandHandler>()
        .AddScoped<ICommandHandler<NotificarLineaFalloTecnicoCommand, ResultadoDeNotificacionDto>, NotificarLineaFalloTecnicoCommandHandler>()
        .AddScoped<ICommandHandler<NotificarCotizacionGanadaCommand, ResultadoDeNotificacionDto>, NotificarCotizacionGanadaCommandHandler>();
}
