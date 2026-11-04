using Nexo.Application.CasosDeUso.Priorizacion;
using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Priorizacion;

/// <summary>Recalibra los pesos del score de priorización. Solo Admin: reordena la bandeja de todos. El PUT no espera el recálculo: guarda, publica el aviso y responde.</summary>
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

    /// <summary>Recalibra los pesos y dispara en segundo plano el recálculo del score persistido.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsParametrosScoreDto>> Put(
        [FromBody] clsParametrosScoreDto parametros, CancellationToken ct = default)
        => Ok(await actualizar.Handle(
            new clsActualizarParametrosScoreCommand(parametros, usuarioActual.Id), ct));
}
