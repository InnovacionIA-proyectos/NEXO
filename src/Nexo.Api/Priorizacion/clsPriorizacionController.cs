using Nexo.Application.CasosDeUso.Priorizacion;
using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Priorizacion;

/// <summary>
/// Recalibrar los pesos del score de priorización.
///
/// No existía. La entidad se declaraba "editable por el Admin" desde el inicio y no había
/// forma de editarla, así que el segundo disparador que pide la HU —recalcular cuando
/// cambian los parámetros— no tenía quién lo disparara.
///
/// SOLO Admin. Cambiar estos pesos reordena la bandeja de todo el mundo: le cambia el
/// trabajo a los coordinadores sin que ellos hayan tocado nada. No es una preferencia de
/// pantalla, es política de operación.
///
/// El PUT no espera el recálculo. Guarda, publica el aviso y responde; el refresco de
/// todas las activas corre aparte, con sus reintentos.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/priorizacion/parametros")]
public class clsPriorizacionController(
    IQueryHandler<clsObtenerParametrosScoreQuery, clsParametrosScoreDto> obtener,
    ICommandHandler<clsActualizarParametrosScoreCommand, clsParametrosScoreDto> actualizar,
    ICurrentUserPort usuarioActual)
    : ControllerBase
{
    /// <summary>Los parámetros vigentes.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsParametrosScoreDto>> Get(CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerParametrosScoreQuery(), ct));

    /// <summary>
    /// Recalibra los pesos. Dispara el recálculo del score persistido en segundo plano.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsParametrosScoreDto>> Put(
        [FromBody] clsParametrosScoreDto parametros, CancellationToken ct = default)
        => Ok(await actualizar.Handle(
            new clsActualizarParametrosScoreCommand(parametros, usuarioActual.Id), ct));
}
