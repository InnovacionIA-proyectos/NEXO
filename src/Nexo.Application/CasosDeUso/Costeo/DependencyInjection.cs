using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Costeo;

public static class DependencyInjection
{
    public static IServiceCollection AddCosteoUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<clsCostearLineaCommand, Unit>, clsCostearLineaCommandHandler>();
        services.AddScoped<ICommandHandler<clsRecostearLineaCommand, Unit>, clsRecostearLineaCommandHandler>();
        return services;
    }
}
