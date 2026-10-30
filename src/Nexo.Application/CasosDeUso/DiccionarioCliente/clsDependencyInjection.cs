using Nexo.Application.CasosDeUso.DiccionarioCliente.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.DiccionarioCliente;

public static class clsDependencyInjection
{
    public static IServiceCollection AddDiccionarioClienteUseCases(this IServiceCollection services) => services
        .AddScoped<IQueryHandler<clsListarDiccionarioClienteQuery, IReadOnlyList<clsTerminoClienteDto>>, clsListarDiccionarioClienteQueryHandler>()
        .AddScoped<IQueryHandler<clsObtenerOpcionesDiccionarioClienteQuery, clsOpcionesDiccionarioClienteDto>, clsObtenerOpcionesDiccionarioClienteQueryHandler>()
        .AddScoped<ICommandHandler<clsCrearTerminoClienteCommand, clsTerminoClienteDto>, clsCrearTerminoClienteCommandHandler>()
        .AddScoped<ICommandHandler<clsEditarTerminoClienteCommand, clsTerminoClienteDto>, clsEditarTerminoClienteCommandHandler>()
        .AddScoped<ICommandHandler<clsEliminarTerminoClienteCommand, Unit>, clsEliminarTerminoClienteCommandHandler>();
}
