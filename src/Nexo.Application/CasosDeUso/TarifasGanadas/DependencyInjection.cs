using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.TarifasGanadas;

public static class DependencyInjection
{
    public static IServiceCollection AddTarifasGanadasUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsSugerirTarifaHistoricaQuery, IReadOnlyList<clsBibliotecaItemDto>>, clsSugerirTarifaHistoricaQueryHandler>()
        .AddScoped<IQueryHandler<clsBuscarBibliotecaTarifasQuery, clsPaginaDto<clsBibliotecaItemDto>>, clsBuscarBibliotecaTarifasQueryHandler>();
}
