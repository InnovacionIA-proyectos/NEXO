using CabinaFletes.Api.Dtos;
using CabinaFletes.Api.Errors;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>
/// Rutas "solo SICE-TAC" que demarca el Administrador de Fletes: origen + destino +
/// tipo de vehículo. Las solicitudes que hagan match se cotizan exclusivamente con el
/// piso SICE-TAC y se auto-costean hacia el comercial (esa parte vive en Biblioteca,
/// todavía no migrada — ver PENDIENTES.md).
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/reglas-solo-sicetac")]
public class ReglasSoloSicetacController(
    IQueryHandler<ListarReglasSoloSicetacQuery, IReadOnlyList<ReglaSoloSicetacDto>> listar,
    ICommandHandler<CrearReglaSoloSicetacCommand, ReglaSoloSicetacDto> crear,
    ICommandHandler<EliminarReglaSoloSicetacCommand, bool> eliminar,
    ICommandHandler<ImportarReglasSoloSicetacExcelCommand, ImportarReglasResultDto> importarExcel)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReglaSoloSicetacDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new ListarReglasSoloSicetacQuery(), ct));

    [HttpPost]
    public async Task<ActionResult<ReglaSoloSicetacDto>> Crear([FromBody] CrearReglaSoloSicetacRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new CrearReglaSoloSicetacCommand(req.Origen, req.Destino, req.TipoVehiculo), ct);
        return Created($"/api/reglas-solo-sicetac/{dto.Id}", dto);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
        => await eliminar.Handle(new EliminarReglaSoloSicetacCommand(id), ct) ? NoContent() : NotFound();

    /// <summary>Carga masiva desde Excel (.xlsx) con columnas Origen, Destino, Tipo de vehículo.</summary>
    [HttpPost("importar-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportarReglasResultDto>> ImportarExcel(IFormFile archivo, CancellationToken ct = default)
    {
        await using var stream = ExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new ImportarReglasSoloSicetacExcelCommand(stream), ct));
    }
}
