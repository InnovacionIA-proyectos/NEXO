using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>
/// Se registra cuando falta <c>Cubicador3D:OutboundBaseUrl</c> y/o
/// <c>Cubicador3D:OutboundApiKey</c>: POST /api/cubicacion responde 503 accionable en vez
/// de fallar la inyección de dependencias — mismo criterio que
/// <c>clsBibliotecaSicetacNoConfiguradoAdapter</c>/<c>clsSapHistoricoNoConfiguradoAdapter</c>.
/// </summary>
public sealed class clsCubicacionRapidaNoConfiguradoAdapter : ICubicacionRapidaPort
{
    public Task<clsCubicacionRapidaResultado> EvaluarAsync(clsCubicacionRapidaSolicitud solicitud, CancellationToken ct = default)
        => throw new clsServiceUnavailableException(
            "CUBICACION_RAPIDA_NO_CONFIGURADA",
            "La cubicación rápida no está configurada. Define Cubicador3D:OutboundBaseUrl y Cubicador3D:OutboundApiKey.");
}
