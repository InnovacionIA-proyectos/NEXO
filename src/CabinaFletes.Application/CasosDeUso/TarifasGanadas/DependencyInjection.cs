using CabinaFletes.Application.CasosDeUso.TarifasGanadas.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.TarifasGanadas;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasGanadasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<SugerirTarifaHistoricaQuery, IReadOnlyList<BibliotecaItemDto>>, SugerirTarifaHistoricaQueryHandler>()
        .AddScoped<IQueryHandler<BuscarBibliotecaTarifasQuery, PaginaDto<BibliotecaItemDto>>, BuscarBibliotecaTarifasQueryHandler>();
}
