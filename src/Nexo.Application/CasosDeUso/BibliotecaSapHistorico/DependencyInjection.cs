using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.BibliotecaSapHistorico;

public static class DependencyInjection
{
    /// <summary>
    /// Biblioteca — histórico y tarifas de plaza de SAP TM leídos en vivo (SOLO
    /// LECTURA) del datacenter corporativo. Complementa a AjustesVigenciaPlaza
    /// (que solo tiene el override LOCAL de vigencia, no el listado/filtros en
    /// sí) y a Biblioteca (que consulta la matriz SICE-TAC, un DW distinto).
    /// </summary>
    public static IServiceCollection AddBibliotecaSapHistoricoUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto>, clsBuscarSapHistoricoQueryHandler>();
        services.AddScoped<IQueryHandler<clsFiltrosSapHistoricoQuery, clsSapHistoricoFiltrosDto>, clsFiltrosSapHistoricoQueryHandler>();
        services.AddScoped<IQueryHandler<clsBuscarSapPlazaQuery, clsSapHistoricoResultDto>, clsBuscarSapPlazaQueryHandler>();
        services.AddScoped<IQueryHandler<clsFiltrosSapPlazaQuery, clsSapHistoricoFiltrosDto>, clsFiltrosSapPlazaQueryHandler>();
        return services;
    }
}
