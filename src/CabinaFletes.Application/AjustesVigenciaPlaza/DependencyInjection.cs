using CabinaFletes.Application.AjustesVigenciaPlaza.Commands;
using CabinaFletes.Application.AjustesVigenciaPlaza.Dtos;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Application.AjustesVigenciaPlaza;

public static class DependencyInjection
{
    public static IServiceCollection AddAjustesVigenciaPlazaUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<AjustarVigenciaPlazaCommand, Unit>, AjustarVigenciaPlazaCommandHandler>()
        .AddScoped<ICommandHandler<AjustarVigenciaPlazaMasivoCommand, AjusteVigenciaResultDto>, AjustarVigenciaPlazaMasivoCommandHandler>()
        .AddScoped<ICommandHandler<QuitarVigenciaPlazaCommand, bool>, QuitarVigenciaPlazaCommandHandler>();
}
