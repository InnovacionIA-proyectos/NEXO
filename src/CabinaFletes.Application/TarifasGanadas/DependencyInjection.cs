using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.TarifasGanadas.Dtos;
using CabinaFletes.Application.TarifasGanadas.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.TarifasGanadas;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasGanadasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<SugerirTarifaHistoricaQuery, IReadOnlyList<BibliotecaItemDto>>, SugerirTarifaHistoricaQueryHandler>()
        .AddScoped<IQueryHandler<BuscarBibliotecaTarifasQuery, IReadOnlyList<BibliotecaItemDto>>, BuscarBibliotecaTarifasQueryHandler>();
}
