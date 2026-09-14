using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Cubicacion.Interfaces;
using CabinaFletes.Application.Common.Exceptions;

namespace CabinaFletes.Infrastructure.Cubicacion.Adaptadores;

/// <summary>
/// Se registra cuando falta <c>Cubicador3D:OutboundBaseUrl</c> y/o
/// <c>Cubicador3D:OutboundApiKey</c>: POST /api/cubicacion responde 503 accionable en vez
/// de fallar la inyección de dependencias — mismo criterio que
/// <c>BibliotecaSicetacNoConfiguradoAdapter</c>/<c>SapHistoricoNoConfiguradoAdapter</c>.
/// </summary>
public sealed class CubicacionRapidaNoConfiguradoAdapter : ICubicacionRapidaPort
{
    public Task<CubicacionRapidaResultado> EvaluarAsync(CubicacionRapidaSolicitud solicitud, CancellationToken ct = default)
        => throw new ServiceUnavailableException(
            "CUBICACION_RAPIDA_NO_CONFIGURADA",
            "La cubicación rápida no está configurada. Define Cubicador3D:OutboundBaseUrl y Cubicador3D:OutboundApiKey.");
}
