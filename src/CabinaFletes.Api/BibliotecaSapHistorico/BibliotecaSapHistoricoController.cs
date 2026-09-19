using System.ComponentModel.DataAnnotations;
using CabinaFletes.Api.Common.RateLimiting;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CabinaFletes.Api.BibliotecaSapHistorico;

/// <summary>
/// Biblioteca — histórico de tarifas SAP TM leído en vivo (SOLO LECTURA) del
/// datacenter corporativo (log de cambios dbsolucionesTI.dbo.tblSAPTarifasLog).
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sap-historico")]
public class BibliotecaSapHistoricoController(
    IQueryHandler<BuscarSapHistoricoQuery, SapHistoricoResultDto> buscar,
    IQueryHandler<FiltrosSapHistoricoQuery, SapHistoricoFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta el histórico ordenado por vigencia descendente, con filtros por
    /// texto (código DIVIPOLA de origen/destino o detalles) y línea de negocio.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<SapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new BuscarSapHistoricoQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en el histórico).</summary>
    [HttpGet("filtros")]
    public async Task<ActionResult<SapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new FiltrosSapHistoricoQuery(), ct));
}
