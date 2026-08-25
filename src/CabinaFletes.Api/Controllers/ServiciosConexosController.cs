using System.ComponentModel.DataAnnotations;
using CabinaFletes.Api.Dtos;
using CabinaFletes.Api.Errors;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.ServiciosConexos.Commands;
using CabinaFletes.Application.ServiciosConexos.Dtos;
using CabinaFletes.Application.ServiciosConexos.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>Catálogo de servicios conexos (logísticos/adicionales) mantenido por Servicios Logísticos.</summary>
[ApiController]
[Authorize]
[Route("api/servicios-conexos")]
public class ServiciosConexosController(
    IQueryHandler<ListarServiciosConexosQuery, PaginaServiciosDto> listar,
    ICommandHandler<CrearServicioConexoCommand, ServicioConexoDto> crear,
    ICommandHandler<EditarServicioConexoCommand, ServicioConexoDto> editar,
    ICommandHandler<ImportarServiciosConexosExcelCommand, ImportarServiciosResultDto> importarExcel)
    : ControllerBase
{
    /// <summary>
    /// Listar el catálogo de servicios conexos, con búsqueda por nombre (q), filtro por
    /// categoría y paginación. Devuelve la página y el total que cumple el filtro.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginaServiciosDto>> Listar(
        [FromQuery] bool soloActivos = false, [FromQuery] string? q = null, [FromQuery] string? categoria = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await listar.Handle(new ListarServiciosConexosQuery(soloActivos, q, categoria, page, pageSize), ct));

    /// <summary>Crear un servicio conexo (solo Servicios Logísticos / Admin).</summary>
    [HttpPost]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    public async Task<ActionResult<ServicioConexoDto>> Crear([FromBody] CrearServicioConexoRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new CrearServicioConexoCommand(
            req.Nombre, req.Categoria, req.ReglaCobro, req.ValorBase, req.VigenteHasta, req.Activo), ct);
        return Created($"/api/servicios-conexos/{dto.Id}", dto);
    }

    /// <summary>Actualizar regla/valor/vigencia de un servicio conexo (solo Servicios Logísticos / Admin).</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    public async Task<ActionResult<ServicioConexoDto>> Editar(Guid id, [FromBody] EditarServicioConexoRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(new EditarServicioConexoCommand(
            id, req.Nombre, req.Categoria, req.ReglaCobro, req.ValorBase, req.VigenteHasta, req.Activo), ct));

    /// <summary>Carga masiva desde Excel (.xlsx): columnas Nombre, Valor base, Vigente hasta, Categoría, Activo.</summary>
    [HttpPost("importar-excel")]
    [Authorize(Roles = "ServiciosLogisticos,AdminTarifas,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportarServiciosResultDto>> ImportarExcel(IFormFile archivo, CancellationToken ct = default)
    {
        await using var stream = ExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new ImportarServiciosConexosExcelCommand(stream), ct));
    }
}
