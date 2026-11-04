using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Priorizacion;

/// <summary>Módulo Priorización: administración de los parámetros del score y refresco del valor persistido.</summary>
public static class clsDependencyInjection
{
    public static IServiceCollection AddPriorizacionUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsObtenerParametrosScoreQuery, clsParametrosScoreDto>,
            clsObtenerParametrosScoreQueryHandler>();
        services.AddScoped<ICommandHandler<clsActualizarParametrosScoreCommand, clsParametrosScoreDto>,
            clsActualizarParametrosScoreCommandHandler>();
        services.AddScoped<ICommandHandler<clsRecalcularScoresActivosCommand, clsResultadoDeRecalculoDto>,
            clsRecalcularScoresActivosCommandHandler>();
        return services;
    }
}
