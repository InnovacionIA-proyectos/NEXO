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
    ICommandHandler<clsImportarServiciosConexosExcelCommand, clsImportarServiciosResultDto> importarExcel)
    : ControllerBase
{
    /// <summary>
    /// Listar el catálogo de servicios conexos, con búsqueda por nombre (q), filtro por
    /// categoría y paginación. Devuelve la página y el total que cumple el filtro.
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
            req.Nombre, req.Categoria, req.ReglaCobro, req.ValorBase, req.VigenteHasta, req.Activo), ct);
        return Created($"/api/servicios-conexos/{dto.Id}", dto);
    }

    /// <summary>Actualizar regla/valor/vigencia de un servicio conexo (solo Servicios Logísticos / Admin).
    /// Actualización parcial: solo se cambian los campos enviados.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsServicioConexoDto>> Editar(Guid id, [FromBody] clsEditarServicioConexoRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(new clsEditarServicioConexoCommand(
            id, req.Nombre, req.Categoria, req.ReglaCobro, req.ValorBase, req.VigenteHasta, req.Activo), ct));

    /// <summary>Carga masiva desde Excel (.xlsx): columnas Nombre, Valor base, Vigente hasta, Categoría, Activo.</summary>
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
}
