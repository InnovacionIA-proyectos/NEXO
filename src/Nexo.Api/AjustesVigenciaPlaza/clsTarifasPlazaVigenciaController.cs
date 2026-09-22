using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.AjustesVigenciaPlaza;

/// <summary>
/// Ajuste LOCAL de la vigencia de tarifas de plaza de SAP TM (la fuente SAP es de solo
/// lectura). Mismas rutas que el proyecto original bajo /api/biblioteca-sap-plaza; el
/// listado/filtros de tarifas de plaza en sí (GET, GET /filtros) pertenecen a
/// BibliotecaSapHistoricoService, que todavía no se migró.
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
[Route("api/biblioteca-sap-plaza")]
public class clsTarifasPlazaVigenciaController(
    ICommandHandler<clsAjustarVigenciaPlazaCommand, Unit> ajustar,
    ICommandHandler<clsAjustarVigenciaPlazaMasivoCommand, clsAjusteVigenciaResultDto> ajustarMasivo,
    ICommandHandler<clsQuitarVigenciaPlazaCommand, bool> quitar)
    : ControllerBase
{
    /// <summary>
    /// Ajusta 1 a 1 la vigencia de una tarifa de plaza (override local, no toca SAP).
    /// NuevaVigencia es obligatoria (ISO-8601, p.ej. "2026-12-31") — 400 VALIDATION_ERROR
    /// si falta o es null. No valida que tarifaId exista en la fuente SAP: el override se
    /// guarda igual, quedará huérfano si el id no corresponde a ninguna tarifa real.
    /// </summary>
    [HttpPatch("{tarifaId:int}/vigencia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AjustarVigencia(int tarifaId, [FromBody] clsAjustarVigenciaPlazaRequest req, CancellationToken ct = default)
    {
        await ajustar.Handle(new clsAjustarVigenciaPlazaCommand(tarifaId, req.NuevaVigencia, req.Motivo), ct);
        return NoContent();
    }

    /// <summary>
    /// Ajuste MASIVO de vigencia (fecha fija o extensión por días/meses) sobre varias
    /// tarifas. 400 VALIDATION_ERROR si TarifaIds/Modo no vienen; además, con Modo="fecha"
    /// NuevaVigencia es obligatoria (400 FECHA_REQUERIDA) y con Modo="extender" lo son
    /// Cantidad (&gt;0) y Unidad ("dias"|"meses") (400 CANTIDAD_INVALIDA/UNIDAD_REQUERIDA).
    /// </summary>
    [HttpPost("vigencia/masivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAjusteVigenciaResultDto>> AjustarVigenciaMasivo(
        [FromBody] clsAjusteVigenciaMasivoRequest req, CancellationToken ct = default)
        => Ok(await ajustarMasivo.Handle(new clsAjustarVigenciaPlazaMasivoCommand(
            req.TarifaIds, req.Modo, req.NuevaVigencia, req.Cantidad, req.Unidad, req.Motivo), ct));

    /// <summary>Quita el ajuste: la tarifa vuelve a regirse por la vigencia de SAP. 404 si
    /// tarifaId no tiene ningún ajuste local vigente para quitar.</summary>
    [HttpDelete("{tarifaId:int}/vigencia")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuitarVigencia(int tarifaId, CancellationToken ct = default)
        => await quitar.Handle(new clsQuitarVigenciaPlazaCommand(tarifaId), ct) ? NoContent() : NotFound();
}
