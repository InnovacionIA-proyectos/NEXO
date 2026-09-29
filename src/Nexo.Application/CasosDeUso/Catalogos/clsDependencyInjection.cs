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
        return services;
    }
}
