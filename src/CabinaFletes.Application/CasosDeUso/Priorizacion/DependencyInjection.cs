using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion;

/// <summary>
/// Módulo Priorización: los parámetros del score y el refresco del valor persistido.
///
/// El cálculo en sí es Domain (<c>ScorePrioridadCalculator</c>) y no se toca. Lo que
/// vive aquí es la administración de sus parámetros y el trabajo de refresco que antes
/// estaba en un BackgroundService del API.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPriorizacionUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<ObtenerParametrosScoreQuery, ParametrosScoreDto>,
            ObtenerParametrosScoreQueryHandler>();
        services.AddScoped<ICommandHandler<ActualizarParametrosScoreCommand, ParametrosScoreDto>,
            ActualizarParametrosScoreCommandHandler>();
        services.AddScoped<ICommandHandler<RecalcularScoresActivosCommand, ResultadoDeRecalculoDto>,
            RecalcularScoresActivosCommandHandler>();
        return services;
    }
}
