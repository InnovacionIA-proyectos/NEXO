using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.Biblioteca;

public static class DependencyInjection
{
    /// <summary>
    /// Módulo Biblioteca: motor de propuesta de tarifa (plaza SAP + SICE-TAC vivo +
    /// histórico SAP) y consulta de solo lectura a la matriz SICE-TAC del DW. Cierra
    /// el bloqueo de "Aplicar propuestas" que dejó pendiente la migración de las
    /// "2 chicas" (AjustesVigenciaPlaza / ReglasSoloSicetac): ITarifarioSapPort e
    /// IPropuestaTarifaPort (Common) reemplazan al PropuestaTarifaNoMigradaAdapter.
    /// </summary>
    public static IServiceCollection AddBibliotecaUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<ProponerPropuestaTarifaQuery, PropuestaSolicitudDto>, ProponerPropuestaTarifaQueryHandler>();
        services.AddScoped<IQueryHandler<BuscarMatrizSicetacQuery, MatrizSicetacResultDto>, BuscarMatrizSicetacQueryHandler>();
        services.AddScoped<IQueryHandler<PeriodosSicetacQuery, IReadOnlyList<int>>, PeriodosSicetacQueryHandler>();
        services.AddScoped<IQueryHandler<FiltrosSicetacQuery, SicetacFiltrosDto>, FiltrosSicetacQueryHandler>();
        return services;
    }
}
