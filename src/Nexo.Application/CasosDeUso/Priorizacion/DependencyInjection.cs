using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Priorizacion;

/// <summary>
/// Módulo Priorización: los parámetros del score y el refresco del valor persistido.
///
/// El cálculo en sí es Domain (<c>clsScorePrioridadCalculator</c>) y no se toca. Lo que
/// vive aquí es la administración de sus parámetros y el trabajo de refresco que antes
/// estaba en un BackgroundService del API.
/// </summary>
public static class DependencyInjection
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
