using Nexo.Application.CasosDeUso.Catalogos.DTOs;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.Catalogos;

public static class clsDependencyInjection
{
    public static IServiceCollection AddCatalogosUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsListarTiposVehiculoQuery, IReadOnlyList<clsTipoVehiculoDto>>, clsListarTiposVehiculoQueryHandler>();
        services.AddScoped<IQueryHandler<clsListarLineasNegocioQuery, IReadOnlyList<clsLineaNegocioDto>>, clsListarLineasNegocioQueryHandler>();
        services.AddScoped<IQueryHandler<clsListarClasesViajeQuery, IReadOnlyList<clsClaseViajeDto>>, clsListarClasesViajeQueryHandler>();
        services.AddScoped<IQueryHandler<clsListarClasesContenedorQuery, IReadOnlyList<clsClaseContenedorDto>>, clsListarClasesContenedorQueryHandler>();
        // S6-HU-27.2/#321 (diccionario de alias) y S6-HU-27.3/#322 (bandeja de normalización de vehículos).
        services.AddScoped<IQueryHandler<clsListarAliasQuery, IReadOnlyList<clsAliasDto>>, clsListarAliasQueryHandler>();
        services.AddScoped<IQueryHandler<clsResolverAliasQuery, clsResolverAliasDto>, clsResolverAliasQueryHandler>();
        services.AddScoped<ICommandHandler<clsCrearAliasCommand, clsAliasDto>, clsCrearAliasCommandHandler>();
        services.AddScoped<IQueryHandler<clsListarVehiculosCuarentenaQuery, IReadOnlyList<clsVehiculoCuarentenaDto>>, clsListarVehiculosCuarentenaQueryHandler>();
        services.AddScoped<ICommandHandler<clsMapearVehiculoCuarentenaCommand, clsMapeoVehiculoDto>, clsMapearVehiculoCuarentenaCommandHandler>();
        services.AddScoped<ICommandHandler<clsDescartarVehiculoCuarentenaCommand, Unit>, clsDescartarVehiculoCuarentenaCommandHandler>();
        return services;
    }
}
