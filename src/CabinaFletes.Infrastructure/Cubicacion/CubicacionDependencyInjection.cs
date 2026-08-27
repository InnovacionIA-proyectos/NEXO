using CabinaFletes.Application.Common.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Registro del puente hacia Cubicador (issue #28, S2-HU-075). Separado de
/// AddMensajeria a propósito: esto NO es MassTransit, es una conexión de
/// RabbitMQ.Client independiente - ver CubicacionRabbitAdapter/
/// CubicacionResultadosConsumidor para el porqué.
/// </summary>
public static class CubicacionDependencyInjection
{
    public static IServiceCollection AddCubicacion(this IServiceCollection services)
    {
        services.AddSingleton<CubicacionRabbitAdapter>();
        services.AddSingleton<ICubicacionPort>(sp => sp.GetRequiredService<CubicacionRabbitAdapter>());
        services.AddHostedService<CubicacionResultadosConsumidor>();
        return services;
    }
}
