using CabinaFletes.Api.Dtos;
using CabinaFletes.Application.AjustesVigenciaPlaza.Commands;
using CabinaFletes.Application.AjustesVigenciaPlaza.Dtos;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>
/// Ajuste LOCAL de la vigencia de tarifas de plaza de SAP TM (la fuente SAP es de solo
/// lectura). Mismas rutas que el proyecto original bajo /api/biblioteca-sap-plaza; el
/// listado/filtros de tarifas de plaza en sí (GET, GET /filtros) pertenecen a
/// BibliotecaSapHistoricoService, que todavía no se migró — ver PENDIENTES.md.
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
[Route("api/biblioteca-sap-plaza")]
public class TarifasPlazaVigenciaController(
    ICommandHandler<AjustarVigenciaPlazaCommand, Unit> ajustar,
    ICommandHandler<AjustarVigenciaPlazaMasivoCommand, AjusteVigenciaResultDto> ajustarMasivo,
    ICommandHandler<QuitarVigenciaPlazaCommand, bool> quitar)
    : ControllerBase
{
    /// <summary>Ajusta 1 a 1 la vigencia de una tarifa de plaza (override local, no toca SAP).</summary>
    [HttpPatch("{tarifaId:int}/vigencia")]
    public async Task<IActionResult> AjustarVigencia(int tarifaId, [FromBody] AjustarVigenciaPlazaRequest req, CancellationToken ct = default)
    {
        await ajustar.Handle(new AjustarVigenciaPlazaCommand(tarifaId, req.NuevaVigencia, req.Motivo), ct);
        return NoContent();
    }

    /// <summary>Ajuste MASIVO de vigencia (fecha fija o extensión por días/meses) sobre varias tarifas.</summary>
    [HttpPost("vigencia/masivo")]
    public async Task<ActionResult<AjusteVigenciaResultDto>> AjustarVigenciaMasivo(
        [FromBody] AjusteVigenciaMasivoRequest req, CancellationToken ct = default)
        => Ok(await ajustarMasivo.Handle(new AjustarVigenciaPlazaMasivoCommand(
            req.TarifaIds, req.Modo, req.NuevaVigencia, req.Cantidad, req.Unidad, req.Motivo), ct));

    /// <summary>Quita el ajuste: la tarifa vuelve a regirse por la vigencia de SAP.</summary>
    [HttpDelete("{tarifaId:int}/vigencia")]
    public async Task<IActionResult> QuitarVigencia(int tarifaId, CancellationToken ct = default)
        => await quitar.Handle(new QuitarVigenciaPlazaCommand(tarifaId), ct) ? NoContent() : NotFound();
}
