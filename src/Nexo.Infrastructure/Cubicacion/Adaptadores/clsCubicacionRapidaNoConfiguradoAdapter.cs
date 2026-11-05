using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>Se registra sin <c>Cubicador3D:OutboundBaseUrl</c> o <c>RecomendacionesApiKey</c>: responde 503 accionable en vez de fallar la DI.</summary>
public sealed class clsCubicacionRapidaNoConfiguradoAdapter : ICubicacionRapidaPort
{
    public Task<clsRecomendacionDto> CrearAsync(clsCrearRecomendacionInput solicitud, CancellationToken ct = default) => NoConfigurada();

    public Task<clsRecomendacionDto> ConsultarAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default) => NoConfigurada();

    private static Task<clsRecomendacionDto> NoConfigurada() => throw new clsServiceUnavailableException(
        "CUBICACION_RAPIDA_NO_CONFIGURADA",
        "La cubicación rápida no está configurada. Define Cubicador3D:OutboundBaseUrl y Cubicador3D:RecomendacionesApiKey.");
}
