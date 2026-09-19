using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;

public static class DependencyInjection
{
    public static IServiceCollection AddAjustesVigenciaPlazaUseCases(this IServiceCollection services) => services
        .AddScoped<ICommandHandler<clsAjustarVigenciaPlazaCommand, Unit>, clsAjustarVigenciaPlazaCommandHandler>()
        .AddScoped<ICommandHandler<clsAjustarVigenciaPlazaMasivoCommand, clsAjusteVigenciaResultDto>, clsAjustarVigenciaPlazaMasivoCommandHandler>()
        .AddScoped<ICommandHandler<clsQuitarVigenciaPlazaCommand, bool>, clsQuitarVigenciaPlazaCommandHandler>();
}
