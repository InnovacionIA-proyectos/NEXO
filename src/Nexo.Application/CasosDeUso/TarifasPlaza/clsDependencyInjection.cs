using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.TarifasPlaza;

public static class clsDependencyInjection
{
    public static IServiceCollection AddTarifasPlazaUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsListarTarifasPlazaQuery, IReadOnlyList<clsTarifaPlazaDto>>, clsListarTarifasPlazaQueryHandler>();
        services.AddScoped<IQueryHandler<clsExportarTarifasPlazaExcelQuery, clsArchivoDto>, clsExportarTarifasPlazaExcelQueryHandler>();
        services.AddScoped<ICommandHandler<clsCrearTarifaPlazaCommand, clsTarifaPlazaDto>, clsCrearTarifaPlazaCommandHandler>();
        services.AddScoped<ICommandHandler<clsEditarTarifaPlazaCommand, clsTarifaPlazaDto>, clsEditarTarifaPlazaCommandHandler>();
        services.AddScoped<ICommandHandler<clsEliminarTarifaPlazaCommand, bool>, clsEliminarTarifaPlazaCommandHandler>();
        services.AddScoped<ICommandHandler<clsImportarTarifasPlazaExcelCommand, clsImportarTarifasPlazaResultDto>, clsImportarTarifasPlazaExcelCommandHandler>();
        return services;
    }
}
