using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public static class clsDependencyInjection
{
    /// <summary>Histórico y tarifas de plaza de SAP TM leídos en vivo (solo lectura) del datacenter corporativo.</summary>
    public static IServiceCollection AddBibliotecaSapHistoricoUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto>, clsBuscarSapHistoricoQueryHandler>();
        services.AddScoped<IQueryHandler<clsFiltrosSapHistoricoQuery, clsSapHistoricoFiltrosDto>, clsFiltrosSapHistoricoQueryHandler>();
        services.AddScoped<IQueryHandler<clsBuscarSapPlazaQuery, clsSapHistoricoResultDto>, clsBuscarSapPlazaQueryHandler>();
        services.AddScoped<IQueryHandler<clsFiltrosSapPlazaQuery, clsSapHistoricoFiltrosDto>, clsFiltrosSapPlazaQueryHandler>();
        return services;
    }
}
