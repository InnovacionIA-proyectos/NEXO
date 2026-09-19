using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasSicetacAdminUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsConsultarTarifasSicetacQuery, IReadOnlyList<clsTarifaSicetacDto>>, clsConsultarTarifasSicetacQueryHandler>()
        .AddScoped<IQueryHandler<clsListarImportacionesSicetacQuery, IReadOnlyList<clsImportacionDto>>, clsListarImportacionesSicetacQueryHandler>()
        .AddScoped<ICommandHandler<clsImportarTarifasSicetacExcelCommand, clsImportResumenDto>, clsImportarTarifasSicetacExcelCommandHandler>();
}
