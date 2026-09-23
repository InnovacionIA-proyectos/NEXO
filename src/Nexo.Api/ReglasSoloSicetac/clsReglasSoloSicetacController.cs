using Nexo.Api.Common.Errors;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.ReglasSoloSicetac;

/// <summary>
/// Rutas "solo SICE-TAC" que demarca el Administrador de Fletes: origen + destino +
/// tipo de vehículo. Las solicitudes que hagan match se cotizan exclusivamente con el
/// piso SICE-TAC y se auto-costean hacia el comercial (esa parte vive en Biblioteca,
/// todavía no migrada).
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/reglas-solo-sicetac")]
public class clsReglasSoloSicetacController(
    IQueryHandler<clsListarReglasSoloSicetacQuery, IReadOnlyList<clsReglaSoloSicetacDto>> listar,
    ICommandHandler<clsCrearReglaSoloSicetacCommand, clsReglaSoloSicetacDto> crear,
    ICommandHandler<clsEliminarReglaSoloSicetacCommand, bool> eliminar,
    ICommandHandler<clsImportarReglasSoloSicetacExcelCommand, clsImportarReglasResultDto> importarExcel)
    : ControllerBase
{
    /// <summary>Lista todas las rutas "solo SICE-TAC" demarcadas.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsReglaSoloSicetacDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarReglasSoloSicetacQuery(), ct));

    /// <summary>Crea una regla "solo SICE-TAC" (origen + destino + tipo de vehículo).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsReglaSoloSicetacDto>> Crear([FromBody] clsCrearReglaSoloSicetacRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearReglaSoloSicetacCommand(req.Origen, req.Destino, req.TipoVehiculo), ct);
        return Created($"/api/reglas-solo-sicetac/{dto.Id}", dto);
    }

    /// <summary>Elimina una regla "solo SICE-TAC" por su id.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        if (!await eliminar.Handle(new clsEliminarReglaSoloSicetacCommand(id), ct))
            throw new clsNotFoundException("REGLA_NO_ENCONTRADA", "No existe una regla 'solo SICE-TAC' con ese identificador.");
        return NoContent();
    }

    /// <summary>Carga masiva desde Excel (.xlsx) con columnas Origen, Destino, Tipo de vehículo.</summary>
    [HttpPost("importar-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsImportarReglasResultDto>> ImportarExcel(IFormFile archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new clsImportarReglasSoloSicetacExcelCommand(stream), ct));
    }
}
