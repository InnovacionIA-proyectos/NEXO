using CabinaFletes.Application.CasosDeUso.ServiciosConexos.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.CasosDeUso.ServiciosConexos;

public static class DependencyInjection
{
    public static IServiceCollection AddServiciosConexosUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CrearServicioConexoCommand, ServicioConexoDto>, CrearServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<EditarServicioConexoCommand, ServicioConexoDto>, EditarServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<ImportarServiciosConexosExcelCommand, ImportarServiciosResultDto>, ImportarServiciosConexosExcelCommandHandler>()
        .AddScoped<IQueryHandler<ListarServiciosConexosQuery, PaginaServiciosDto>, ListarServiciosConexosQueryHandler>();
}
