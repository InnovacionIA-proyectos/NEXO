using Nexo.Api.Common.Auth;
using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.PlanDeCargue;

/// <summary>
/// Puente de solo lectura hacia el microservicio Cubicador (Python) — NO es un
/// controlador de uso interno/frontend. Autenticación por Bearer estático de
/// servicio-a-servicio (<see cref="clsCubicadorApiKeyAuthenticationHandler"/>), no por
/// sesión de usuario. Ver cubicador/CONTRATO.md sección 7.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = clsCubicadorApiKeyAuthenticationHandler.SchemeName)]
[Route("api/lineas-solicitud")]
public class clsLineasSolicitudCubicacionController(
    IQueryHandler<clsObtenerCubicacionLineaQuery, clsLineaParaCubicacionDto> obtenerCubicacionLinea)
    : ControllerBase
{
    /// <summary>Posiciones (piezas/bultos) de la línea + origen/destino de su ruta, para
    /// que el Cubicador arme el plan de cargue.</summary>
    [HttpGet("{id:guid}/cubicacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsLineaParaCubicacionDto>> Cubicacion(Guid id, CancellationToken ct = default)
        => Ok(await obtenerCubicacionLinea.Handle(new clsObtenerCubicacionLineaQuery(id), ct));
}
