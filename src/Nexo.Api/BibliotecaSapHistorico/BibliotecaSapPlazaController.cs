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
/// Biblioteca — tarifas de plaza (vigentes) de SAP TM leídas en vivo (SOLO LECTURA)
/// del datacenter corporativo (dbsolucionesTI.dbo.tblSAPTarifas). Misma estructura
/// que el histórico; los códigos se resuelven con los catálogos SAP y DANE. El
/// ajuste LOCAL de vigencia (PATCH/POST/DELETE) vive en
/// <see cref="TarifasPlazaVigenciaController"/> (módulo AjustesVigenciaPlaza),
/// misma ruta base — no hay colisión porque los verbos/subrutas no se solapan.
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sap-plaza")]
public class BibliotecaSapPlazaController(
    IQueryHandler<BuscarSapPlazaQuery, SapHistoricoResultDto> buscar,
    IQueryHandler<FiltrosSapPlazaQuery, SapHistoricoFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta las tarifas de plaza ordenadas por vigencia descendente, con filtros
    /// por texto (código DIVIPOLA de origen/destino o detalles), línea de negocio,
    /// vehículo, carrocería y clase de viaje.
    ///
    /// Issue #156: medido en vivo, ~22,8 K filas — "top" nunca dejaba ver más allá
    /// de las primeras 50-200, sin aviso de que el resto existía, aunque `total`
    /// ya viajaba en la respuesta. "page" (1-based, opcional, default 1) agrega
    /// el `Skip` que faltaba; "top" sigue siendo el tamaño de página. Mismo shape
    /// de respuesta que siempre (`{ total, items }`) — cambio aditivo, no rompe a
    /// quien ya consume esto sin "page".
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.PoliticaListadosPesados)]
    public async Task<ActionResult<SapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new BuscarSapPlazaQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top, page), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en la tabla).</summary>
    [HttpGet("filtros")]
    public async Task<ActionResult<SapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new FiltrosSapPlazaQuery(), ct));
}
