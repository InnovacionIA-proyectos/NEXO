using CabinaFletes.Api.Errors;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>Caché local del piso SICE-TAC (import periódico admin desde Excel).</summary>
[ApiController]
[Authorize]
[Route("api/tarifas-sicetac")]
public class TarifasSicetacController(
    IQueryHandler<ConsultarTarifasSicetacQuery, IReadOnlyList<TarifaSicetacDto>> consultar,
    IQueryHandler<ListarImportacionesSicetacQuery, IReadOnlyList<ImportacionDto>> listarImportaciones,
    ICommandHandler<ImportarTarifasSicetacExcelCommand, ImportResumenDto> importarExcel)
    : ControllerBase
{
    /// <summary>Consultar el piso SICE-TAC por ruta/configuración/carga.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TarifaSicetacDto>>> Consultar(
        [FromQuery] string? origen, [FromQuery] string? destino, [FromQuery] string? config,
        [FromQuery] string? tipoCarga, [FromQuery] string? unidad, [FromQuery] string? condicion,
        [FromQuery] int? periodo, CancellationToken ct = default)
        => Ok(await consultar.Handle(new ConsultarTarifasSicetacQuery(
            new SicetacFiltro(origen, destino, config, tipoCarga, unidad, condicion, periodo)), ct));

    /// <summary>Cargar el Excel SICE-TAC (multipart/form-data, campo "archivo"). Upsert idempotente.</summary>
    [HttpPost("importar")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportResumenDto>> Importar(IFormFile? archivo, CancellationToken ct = default)
    {
        await using var stream = ExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new ImportarTarifasSicetacExcelCommand(stream), ct));
    }

    /// <summary>Historial de cargas de SICE-TAC.</summary>
    [HttpGet("importaciones")]
    public async Task<ActionResult<IReadOnlyList<ImportacionDto>>> Importaciones(CancellationToken ct = default)
        => Ok(await listarImportaciones.Handle(new ListarImportacionesSicetacQuery(), ct));
}
