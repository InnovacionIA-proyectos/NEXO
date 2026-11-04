using System.ComponentModel.DataAnnotations;
using Nexo.Api.AjustesVigenciaPlaza;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.BibliotecaSapHistorico;

/// <summary>
/// Tarifas de plaza vigentes de SAP TM leídas en vivo (solo lectura) de dbsolucionesTI.dbo.tblSAPTarifas; los códigos se resuelven con los catálogos SAP y DANE.
/// El ajuste local de vigencia vive en <see cref="clsTarifasPlazaVigenciaController"/>, con la misma ruta base (los verbos/subrutas no se solapan).
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sap-plaza")]
public class clsBibliotecaSapPlazaController(
    IQueryHandler<clsBuscarSapPlazaQuery, clsSapHistoricoResultDto> buscar,
    IQueryHandler<clsFiltrosSapPlazaQuery, clsSapHistoricoFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta las tarifas de plaza por vigencia descendente, con filtros por texto (DIVIPOLA de origen/destino o detalles), línea de negocio, vehículo, carrocería y clase de viaje.
    /// Paginada: <c>page</c> (base 1, default 1) y <c>top</c> como tamaño de página; <c>total</c> es el total sin paginar.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosPesados)]
    public async Task<ActionResult<clsSapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new clsBuscarSapPlazaQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top, page), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en la tabla).</summary>
    [HttpGet("filtros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsSapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new clsFiltrosSapPlazaQuery(), ct));
}
