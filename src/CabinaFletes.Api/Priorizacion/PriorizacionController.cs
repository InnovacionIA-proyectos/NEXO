using CabinaFletes.Application.CasosDeUso.Priorizacion;
using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Priorizacion;

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
public class PriorizacionController(
    IQueryHandler<ObtenerParametrosScoreQuery, ParametrosScoreDto> obtener,
    ICommandHandler<ActualizarParametrosScoreCommand, ParametrosScoreDto> actualizar,
    ICurrentUserPort usuarioActual)
    : ControllerBase
{
    /// <summary>Los parámetros vigentes.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ParametrosScoreDto>> Get(CancellationToken ct = default)
        => Ok(await obtener.Handle(new ObtenerParametrosScoreQuery(), ct));

    /// <summary>
    /// Recalibra los pesos. Dispara el recálculo del score persistido en segundo plano.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParametrosScoreDto>> Put(
        [FromBody] ParametrosScoreDto parametros, CancellationToken ct = default)
        => Ok(await actualizar.Handle(
            new ActualizarParametrosScoreCommand(parametros, usuarioActual.Id), ct));
}
