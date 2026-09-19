using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.TarifasSicetacAdmin;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasSicetacAdminUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>>, ConsultarTarifasSicetacQueryHandler>()
        .AddScoped<IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>>, ListarImportacionesSicetacQueryHandler>()
        .AddScoped<ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto>, ImportarTarifasSicetacExcelCommandHandler>();
}
