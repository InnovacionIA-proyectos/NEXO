using Nexo.Api.Common.Errors;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin;
using Nexo.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.TarifasSicetacAdmin;

/// <summary>Caché local del piso SICE-TAC (import periódico admin desde Excel).</summary>
[ApiController]
[Authorize]
[Route("api/tarifas-sicetac")]
public class clsTarifasSicetacController(
    IQueryHandler<clsConsultarTarifasSicetacQuery, IReadOnlyList<clsTarifaSicetacDto>> consultar,
    IQueryHandler<clsListarImportacionesSicetacQuery, IReadOnlyList<clsImportacionDto>> listarImportaciones,
    ICommandHandler<clsImportarTarifasSicetacExcelCommand, clsImportResumenDto> importarExcel)
    : ControllerBase
{
    /// <summary>Consultar el piso SICE-TAC por ruta/configuración/carga.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<clsTarifaSicetacDto>>> Consultar(
        [FromQuery] string? origen, [FromQuery] string? destino, [FromQuery] string? config,
        [FromQuery] string? tipoCarga, [FromQuery] string? unidad, [FromQuery] string? condicion,
        [FromQuery] int? periodo, CancellationToken ct = default)
        => Ok(await consultar.Handle(new clsConsultarTarifasSicetacQuery(
            new clsSicetacFiltro(origen, destino, config, tipoCarga, unidad, condicion, periodo)), ct));

    /// <summary>Cargar el Excel SICE-TAC (multipart/form-data, campo "archivo"). Upsert idempotente.</summary>
    [HttpPost("importar")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<clsImportResumenDto>> Importar(IFormFile? archivo, CancellationToken ct = default)
    {
        await using var stream = clsExcelUpload.AbrirValidado(archivo);
        return Ok(await importarExcel.Handle(new clsImportarTarifasSicetacExcelCommand(stream), ct));
    }

    /// <summary>Historial de cargas de SICE-TAC.</summary>
    [HttpGet("importaciones")]
    public async Task<ActionResult<IReadOnlyList<clsImportacionDto>>> Importaciones(CancellationToken ct = default)
        => Ok(await listarImportaciones.Handle(new clsListarImportacionesSicetacQuery(), ct));
}
