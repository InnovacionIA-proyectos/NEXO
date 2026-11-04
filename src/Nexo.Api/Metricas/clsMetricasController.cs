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
    /// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder). <c>desde</c>/<c>hasta</c> opcionales: la cola cuenta solicitudes creadas en el rango y Cumplimiento SLA, cotizaciones entregadas en él.</summary>
    [HttpGet("cumplimiento")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsCumplimientoDto>> Cumplimiento(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, CancellationToken ct = default)
        => Ok(await cumplimiento.Handle(new clsObtenerCumplimientoQuery(desde, hasta), ct));

    /// <summary>
    /// Los 4 KPIs del piloto: tiempo de costeo del pliego, % de líneas auto-costeadas, cotizaciones emitidas desde la app y líneas con flete manual a verificar contra el piso.
    /// Sin fecha corre sobre todo el histórico; <c>tamano</c> ("1-10", "11-100" o "+100", por líneas del pliego) recorta el tiempo de costeo.
    /// </summary>
    [HttpGet("kpis-piloto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsKpisPilotoDto>> KpisPiloto(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, [FromQuery] string? tamano = null,
        CancellationToken ct = default)
        => Ok(await kpisPiloto.Handle(new clsObtenerKpisPilotoQuery(desde, hasta, tamano), ct));
}
