using Nexo.Application.CasosDeUso.Metricas;
using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Metricas;

/// <summary>Tablero del Líder de proceso: KPIs de cumplimiento y del piloto, solo lectura.</summary>
[ApiController]
[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]
[Route("api/metricas")]
public class clsMetricasController(
    IQueryHandler<clsObtenerCumplimientoQuery, clsCumplimientoDto> cumplimiento,
    IQueryHandler<clsObtenerKpisPilotoQuery, clsKpisPilotoDto> kpisPiloto)
    : ControllerBase
{
    /// <summary>
    /// KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).
    ///
    /// S5-HU-21.1/#287: <c>desde</c>/<c>hasta</c> opcionales — el mismo filtro que "KPIs del
    /// Piloto". Las 5 tarjetas de la cola cuentan solicitudes creadas en el rango y Cumplimiento
    /// SLA, cotizaciones entregadas en el rango. Sin ellos, el comportamiento es el de siempre.
    /// </summary>
    [HttpGet("cumplimiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsCumplimientoDto>> Cumplimiento(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, CancellationToken ct = default)
        => Ok(await cumplimiento.Handle(new clsObtenerCumplimientoQuery(desde, hasta), ct));

    /// <summary>
    /// Los 4 KPIs cuantitativos del piloto de 2 semanas (documento de entendimiento,
    /// sección 14): tiempo de costeo del pliego, % de líneas auto-costeadas, cotizaciones
    /// emitidas desde la app y líneas con flete manual a verificar contra el piso.
    /// Sin filtro de fecha, corre sobre todo el histórico.
    ///
    /// S5-HU-21.1/#287: <c>tamano</c> ("1-10", "11-100" o "+100", por total de líneas del pliego)
    /// recorta el tiempo de costeo; sin él, todos los pliegos.
    /// </summary>
    [HttpGet("kpis-piloto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsKpisPilotoDto>> KpisPiloto(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, [FromQuery] string? tamano = null,
        CancellationToken ct = default)
        => Ok(await kpisPiloto.Handle(new clsObtenerKpisPilotoQuery(desde, hasta, tamano), ct));
}
