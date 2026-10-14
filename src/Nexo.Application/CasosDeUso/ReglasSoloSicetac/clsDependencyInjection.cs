using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.ReglasSoloSicetac;

public static class clsDependencyInjection
{
    public static IServiceCollection AddReglasSoloSicetacUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsCrearReglaSoloSicetacCommand, clsReglaSoloSicetacDto>, clsCrearReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarReglaSoloSicetacCommand, bool>, clsEliminarReglaSoloSicetacCommandHandler>()
        .AddScoped<ICommandHandler<clsImportarReglasSoloSicetacExcelCommand, clsImportarReglasResultDto>, clsImportarReglasSoloSicetacExcelCommandHandler>()
        .AddScoped<IQueryHandler<clsListarReglasSoloSicetacQuery, IReadOnlyList<clsReglaSoloSicetacDto>>, clsListarReglasSoloSicetacQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerPlantillaReglasSoloSicetacQuery, clsArchivoDto>, clsObtenerPlantillaReglasSoloSicetacQueryHandler>()
        .AddScoped<IQueryHandler<clsExportarReglasSoloSicetacExcelQuery, clsArchivoDto>, clsExportarReglasSoloSicetacExcelQueryHandler>();
}
