using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.AjustesVigenciaPlaza;

/// <summary>Ajuste local de la vigencia de tarifas de plaza de SAP TM (la fuente SAP es de solo lectura).</summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,AdminBiblioteca,Admin")]
[Route("api/biblioteca-sap-plaza")]
public class clsTarifasPlazaVigenciaController(
    ICommandHandler<clsAjustarVigenciaPlazaCommand, Unit> ajustar,
    ICommandHandler<clsAjustarVigenciaPlazaMasivoCommand, clsAjusteVigenciaResultDto> ajustarMasivo,
    ICommandHandler<clsQuitarVigenciaPlazaCommand, bool> quitar)
    : ControllerBase
{
    /// <summary>
    /// Ajusta 1 a 1 la vigencia de una tarifa de plaza (override local, no toca SAP). No valida que tarifaId exista en SAP: el override queda huérfano si el id no corresponde.
    /// </summary>
    [HttpPatch("{tarifaId:int}/vigencia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AjustarVigencia(int tarifaId, [FromBody] clsAjustarVigenciaPlazaRequest req, CancellationToken ct = default)
    {
        await ajustar.Handle(new clsAjustarVigenciaPlazaCommand(tarifaId, req.NuevaVigencia, req.Motivo), ct);
        return NoContent();
    }

    /// <summary>Ajuste masivo de vigencia (fecha fija o extensión por días/meses) sobre varias tarifas.</summary>
    [HttpPost("vigencia/masivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAjusteVigenciaResultDto>> AjustarVigenciaMasivo(
        [FromBody] clsAjusteVigenciaMasivoRequest req, CancellationToken ct = default)
        => Ok(await ajustarMasivo.Handle(new clsAjustarVigenciaPlazaMasivoCommand(
            req.TarifaIds, req.Modo, req.NuevaVigencia, req.Cantidad, req.Unidad, req.Motivo), ct));

    /// <summary>Quita el ajuste: la tarifa vuelve a regirse por la vigencia de SAP. 404 si no tiene ajuste local.</summary>
    [HttpDelete("{tarifaId:int}/vigencia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuitarVigencia(int tarifaId, CancellationToken ct = default)
    {
        if (!await quitar.Handle(new clsQuitarVigenciaPlazaCommand(tarifaId), ct))
            throw new clsNotFoundException("AJUSTE_VIGENCIA_NO_ENCONTRADO", "La tarifa indicada no tiene ningún ajuste local de vigencia para quitar.");
        return NoContent();
    }
}
