using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.ServiciosConexos;

public static class DependencyInjection
{
    public static IServiceCollection AddServiciosConexosUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsCrearServicioConexoCommand, clsServicioConexoDto>, clsCrearServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<clsEditarServicioConexoCommand, clsServicioConexoDto>, clsEditarServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<clsImportarServiciosConexosExcelCommand, clsImportarServiciosResultDto>, clsImportarServiciosConexosExcelCommandHandler>()
        .AddScoped<IQueryHandler<clsListarServiciosConexosQuery, clsPaginaServiciosDto>, clsListarServiciosConexosQueryHandler>();
}
