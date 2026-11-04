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

/// <summary>Rutas "solo SICE-TAC" (origen + destino + tipo de vehículo): las solicitudes que coinciden se cotizan solo con el piso SICE-TAC y se auto-costean hacia el comercial.</summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,AdminBiblioteca,Admin")]
[Route("api/reglas-solo-sicetac")]
public class clsReglasSoloSicetacController(
    IQueryHandler<clsListarReglasSoloSicetacQuery, IReadOnlyList<clsReglaSoloSicetacDto>> listar,
    ICommandHandler<clsCrearReglaSoloSicetacCommand, clsReglaSoloSicetacDto> crear,
    ICommandHandler<clsEliminarReglaSoloSicetacCommand, bool> eliminar,
    ICommandHandler<clsImportarReglasSoloSicetacExcelCommand, clsImportarReglasResultDto> importarExcel,
    IQueryHandler<clsObtenerPlantillaReglasSoloSicetacQuery, clsArchivoDto> plantillaExcel,
    IQueryHandler<clsExportarReglasSoloSicetacExcelQuery, clsArchivoDto> exportarExcel)
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

    /// <summary>Plantilla Excel descargable para la carga masiva (columnas Origen, Destino, Tipo de vehículo).</summary>
    [HttpGet("plantilla-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlantillaExcel(CancellationToken ct = default)
    {
        var archivo = await plantillaExcel.Handle(new clsObtenerPlantillaReglasSoloSicetacQuery(), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }

    /// <summary>Descarga las reglas cargadas con las columnas de <c>importar-excel</c>, para editarlas y reimportarlas; las existentes se reportan como duplicadas (no se pisan).</summary>
    [HttpGet("exportar-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarExcel(CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new clsExportarReglasSoloSicetacExcelQuery(), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
