using Nexo.Api.Common.Errors;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.TarifasPlaza;
using Nexo.Application.CasosDeUso.TarifasPlaza.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.TarifasPlaza;

/// <summary>
/// CRUD de Tarifas de Plaza gobernadas por Nexo (S4-HU-10.2) — recrea el formulario de
/// captura que hoy vive en un sistema externo, con carga/descarga masiva por Excel.
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,AdminBiblioteca,Admin")]
[Route("api/tarifas-plaza")]
public class clsTarifasPlazaController(
    IQueryHandler<clsListarTarifasPlazaQuery, IReadOnlyList<clsTarifaPlazaDto>> listar,
    ICommandHandler<clsCrearTarifaPlazaCommand, clsTarifaPlazaDto> crear,
    ICommandHandler<clsEditarTarifaPlazaCommand, clsTarifaPlazaDto> editar,
    ICommandHandler<clsEliminarTarifaPlazaCommand, bool> eliminar,
    ICommandHandler<clsImportarTarifasPlazaExcelCommand, clsImportarTarifasPlazaResultDto> importarExcel,
    IQueryHandler<clsExportarTarifasPlazaExcelQuery, clsArchivoDto> exportarExcel)
    : ControllerBase
{
    /// <summary>Lista todas las tarifas de plaza.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsTarifaPlazaDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarTarifasPlazaQuery(), ct));

    /// <summary>Crea una tarifa de plaza.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsTarifaPlazaDto>> Crear([FromBody] clsTarifaPlazaRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearTarifaPlazaCommand(
            req.LineaNegocio, req.ClaseViaje, req.OrigenMunicipioId, req.DestinoMunicipioId,
            req.ClaseContenedor, req.PesoId, req.TipoVehiculo, req.Valor, req.Vigencia, req.DetallesTarifa), ct);
        return Created($"/api/tarifas-plaza/{dto.Id}", dto);
    }

    /// <summary>Edita una tarifa de plaza existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsTarifaPlazaDto>> Editar(Guid id, [FromBody] clsTarifaPlazaRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(new clsEditarTarifaPlazaCommand(
            id, req.LineaNegocio, req.ClaseViaje, req.OrigenMunicipioId, req.DestinoMunicipioId,
            req.ClaseContenedor, req.PesoId, req.TipoVehiculo, req.Valor, req.Vigencia, req.DetallesTarifa), ct));

    /// <summary>Elimina una tarifa de plaza por su id.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        if (!await eliminar.Handle(new clsEliminarTarifaPlazaCommand(id), ct))
            throw new clsNotFoundException("TARIFA_PLAZA_NO_ENCONTRADA", "No existe una tarifa de plaza con ese identificador.");
        return NoContent();
    }

    /// <summary>Carga masiva desde Excel (.xlsx): upsert por línea de negocio + clase de
    /// viaje + origen + destino + clase de contenedor + rango de peso + tipo de vehículo.</summary>
    [HttpPost("importar-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsImportarTarifasPlazaResultDto>> ImportarExcel(IFormFile archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new clsImportarTarifasPlazaExcelCommand(stream), ct));
    }

    /// <summary>Descarga masiva a Excel del catálogo completo — el mismo archivo sirve de plantilla.</summary>
    [HttpGet("exportar-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarExcel(CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new clsExportarTarifasPlazaExcelQuery(), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}

public class clsTarifaPlazaRequest
{
    public string LineaNegocio { get; set; } = null!;
    public string ClaseViaje { get; set; } = null!;
    public int OrigenMunicipioId { get; set; }
    public int DestinoMunicipioId { get; set; }
    public string? ClaseContenedor { get; set; }
    public int? PesoId { get; set; }
    public string? TipoVehiculo { get; set; }
    public decimal Valor { get; set; }
    public DateOnly Vigencia { get; set; }
    /// <summary>Opcional (S6-HU-29.2/#312): vacío o null se guarda como texto vacío. Máx. 2000 caracteres.</summary>
    public string? DetallesTarifa { get; set; }
}
