using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Biblioteca;

public static class clsDependencyInjection
{
    /// <summary>Módulo Biblioteca: motor de propuesta de tarifa (plaza SAP + SICE-TAC vivo + histórico SAP) y consulta de solo lectura a la matriz SICE-TAC del DW.</summary>
    public static IServiceCollection AddBibliotecaUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto>, clsProponerPropuestaTarifaQueryHandler>();
        services.AddScoped<IQueryHandler<clsBuscarMatrizSicetacQuery, clsMatrizSicetacResultDto>, clsBuscarMatrizSicetacQueryHandler>();
        services.AddScoped<IQueryHandler<clsPeriodosSicetacQuery, IReadOnlyList<int>>, clsPeriodosSicetacQueryHandler>();
        services.AddScoped<IQueryHandler<clsFiltrosSicetacQuery, clsSicetacFiltrosDto>, clsFiltrosSicetacQueryHandler>();
        return services;
    }
}
