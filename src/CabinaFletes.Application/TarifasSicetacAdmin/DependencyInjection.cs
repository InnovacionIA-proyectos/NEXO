using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasSicetacAdmin.Commands;
using CabinaFletes.Application.TarifasSicetacAdmin.Dtos;
using CabinaFletes.Application.TarifasSicetacAdmin.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.TarifasSicetacAdmin;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasSicetacAdminUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>>, ConsultarTarifasSicetacQueryHandler>()
        .AddScoped<IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>>, ListarImportacionesSicetacQueryHandler>()
        .AddScoped<ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto>, ImportarTarifasSicetacExcelCommandHandler>();
}
