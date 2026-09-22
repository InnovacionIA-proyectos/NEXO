using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.BibliotecaSapHistorico;

/// <summary>
/// Biblioteca — histórico de tarifas SAP TM leído en vivo (SOLO LECTURA) del
/// datacenter corporativo (log de cambios dbsolucionesTI.dbo.tblSAPTarifasLog).
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sap-historico")]
public class clsBibliotecaSapHistoricoController(
    IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto> buscar,
    IQueryHandler<clsFiltrosSapHistoricoQuery, clsSapHistoricoFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta el histórico ordenado por vigencia descendente, con filtros por
    /// texto (código DIVIPOLA de origen/destino o detalles) y línea de negocio.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsSapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new clsBuscarSapHistoricoQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en el histórico).</summary>
    [HttpGet("filtros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsSapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new clsFiltrosSapHistoricoQuery(), ct));
}
