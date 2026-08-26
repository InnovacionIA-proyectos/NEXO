using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;

public static class DependencyInjection
{
    public static IServiceCollection AddReglasSoloSicetacUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CrearReglaSoloSicetacCommand, ReglaSoloSicetacDto>, CrearReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<EliminarReglaSoloSicetacCommand, bool>, EliminarReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<ImportarReglasSoloSicetacExcelCommand, ImportarReglasResultDto>, ImportarReglasSoloSicetacExcelCommandHandler>()
        .AddScoped<IQueryHandler<ListarReglasSoloSicetacQuery, IReadOnlyList<ReglaSoloSicetacDto>>, ListarReglasSoloSicetacQueryHandler>();
}
