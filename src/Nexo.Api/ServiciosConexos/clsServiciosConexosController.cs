using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.ServiciosConexos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.ServiciosConexos;

/// <summary>Catálogo de servicios conexos (logísticos/adicionales) mantenido por Servicios Logísticos.</summary>
[ApiController]
[Authorize]
[Route("api/servicios-conexos")]
public class clsServiciosConexosController(
    IQueryHandler<clsListarServiciosConexosQuery, clsPaginaServiciosDto> listar,
    ICommandHandler<clsCrearServicioConexoCommand, clsServicioConexoDto> crear,
    ICommandHandler<clsEditarServicioConexoCommand, clsServicioConexoDto> editar,
    ICommandHandler<clsImportarServiciosConexosExcelCommand, clsImportarServiciosResultDto> importarExcel,
    IQueryHandler<clsExportarServiciosConexosExcelQuery, clsArchivoDto> exportarExcel,
    IQueryHandler<clsObtenerPlantillaServicioConexoQuery, clsArchivoDto> plantillaExcel)
    : ControllerBase
{
    /// <summary>
    /// Listar el catálogo de servicios conexos, con búsqueda por tipo/código/dimensiones
    /// (q, S5-HU-18/#262), filtro por categoría y paginación. Devuelve la página y el
    /// total que cumple el filtro.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsPaginaServiciosDto>> Listar(
        [FromQuery] bool soloActivos = false, [FromQuery] string? q = null, [FromQuery] string? categoria = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarServiciosConexosQuery(soloActivos, q, categoria, page, pageSize), ct));

    /// <summary>Crear un servicio conexo (solo Servicios Logísticos / Admin). 409 si ya
    /// existe un servicio activo con el mismo nombre.</summary>
    [HttpPost]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsServicioConexoDto>> Crear([FromBody] clsCrearServicioConexoRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearServicioConexoCommand(
            req.CodigoServicio, req.TipoServicio, req.Origen, req.Destino, req.Puerto, req.Ruta, req.TipoMaquina,
            req.CostoUnitario, req.Unidad, req.Vigencia, req.Categoria, req.Activo), ct);
        return Created($"/api/servicios-conexos/{dto.Id}", dto);
    }

    /// <summary>Actualizar un servicio conexo (solo Servicios Logísticos / Admin).
    /// Actualización parcial: solo se cambian los campos enviados.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsServicioConexoDto>> Editar(Guid id, [FromBody] clsEditarServicioConexoRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(new clsEditarServicioConexoCommand(
            id, req.CodigoServicio, req.TipoServicio, req.Origen, req.Destino, req.Puerto, req.Ruta, req.TipoMaquina,
            req.CostoUnitario, req.Unidad, req.Vigencia, req.Categoria, req.Activo), ct));

    /// <summary>Carga masiva desde Excel (.xlsx): columnas Codigo, Tipo Servicio, Origen,
    /// Destino, Puerto, Ruta, Tipo Maquina, Costo Unitario, Unidad, Vigencia, Categoria, Activo.</summary>
    [HttpPost("importar-excel")]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsImportarServiciosResultDto>> ImportarExcel(IFormFile archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new clsImportarServiciosConexosExcelCommand(stream), ct));
    }

    /// <summary>Descarga masiva a Excel del catálogo — el mismo archivo sirve para editarlo
    /// y reimportarlo como actualización (S4-HU-15.3/#249). S5-HU-19/#261: filtrable por
    /// texto libre (q, sobre tipo/código) y/o categoría (logistico/adicional) — mismos
    /// filtros que GET /api/servicios-conexos; sin filtros, exporta el catálogo completo.</summary>
    [HttpGet("exportar-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarExcel([FromQuery] string? q = null, [FromQuery] string? categoria = null, CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new clsExportarServiciosConexosExcelQuery(q, categoria), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }

    /// <summary>Plantilla de Excel descargable por tipo de servicio (S4-HU-15.3/#249):
    /// almacenamiento, itr, precinto, acompanamiento — cada una con solo sus columnas.</summary>
    [HttpGet("plantilla-excel/{tipo}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlantillaExcel(string tipo, CancellationToken ct = default)
    {
        var archivo = await plantillaExcel.Handle(new clsObtenerPlantillaServicioConexoQuery(tipo), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
