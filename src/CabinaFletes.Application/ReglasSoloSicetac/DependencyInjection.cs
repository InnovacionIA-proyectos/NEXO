using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.ReglasSoloSicetac.Commands;
using CabinaFletes.Application.ReglasSoloSicetac.Dtos;
using CabinaFletes.Application.ReglasSoloSicetac.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.ReglasSoloSicetac;

public static class DependencyInjection
{
    public static IServiceCollection AddReglasSoloSicetacUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CrearReglaSoloSicetacCommand, ReglaSoloSicetacDto>, CrearReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<EliminarReglaSoloSicetacCommand, bool>, EliminarReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<ImportarReglasSoloSicetacExcelCommand, ImportarReglasResultDto>, ImportarReglasSoloSicetacExcelCommandHandler>()
        .AddScoped<IQueryHandler<ListarReglasSoloSicetacQuery, IReadOnlyList<ReglaSoloSicetacDto>>, ListarReglasSoloSicetacQueryHandler>();
}
