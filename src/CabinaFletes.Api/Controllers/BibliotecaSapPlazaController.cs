using System.ComponentModel.DataAnnotations;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

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
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new BuscarSapPlazaQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en la tabla).</summary>
    [HttpGet("filtros")]
    public async Task<ActionResult<SapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new FiltrosSapPlazaQuery(), ct));
}
