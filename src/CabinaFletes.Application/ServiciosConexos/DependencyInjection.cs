using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.ServiciosConexos.Commands;
using CabinaFletes.Application.ServiciosConexos.Dtos;
using CabinaFletes.Application.ServiciosConexos.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.ServiciosConexos;

public static class DependencyInjection
{
    public static IServiceCollection AddServiciosConexosUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<CrearServicioConexoCommand, ServicioConexoDto>, CrearServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<EditarServicioConexoCommand, ServicioConexoDto>, EditarServicioConexoCommandHandler>()
        .AddScoped<ICommandHandler<ImportarServiciosConexosExcelCommand, ImportarServiciosResultDto>, ImportarServiciosConexosExcelCommandHandler>()
        .AddScoped<IQueryHandler<ListarServiciosConexosQuery, PaginaServiciosDto>, ListarServiciosConexosQueryHandler>();
}
