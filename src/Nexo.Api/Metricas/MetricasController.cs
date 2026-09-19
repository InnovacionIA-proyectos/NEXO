using Nexo.Application.CasosDeUso.Metricas;
using Nexo.Application.CasosDeUso.Metricas.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Metricas;

[ApiController]
[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]
[Route("api/metricas")]
public class MetricasController(
    IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto> cumplimiento,
    IQueryHandler<ObtenerKpisPilotoQuery, KpisPilotoDto> kpisPiloto)
    : ControllerBase
{
    /// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
    [HttpGet("cumplimiento")]
    public async Task<ActionResult<CumplimientoDto>> Cumplimiento(CancellationToken ct = default)
        => Ok(await cumplimiento.Handle(new ObtenerCumplimientoQuery(), ct));

    /// <summary>
    /// Los 4 KPIs cuantitativos del piloto de 2 semanas (documento de entendimiento,
    /// sección 14): tiempo de costeo del pliego, % de líneas auto-costeadas, cotizaciones
    /// emitidas desde la app y líneas con flete manual a verificar contra el piso.
    /// Sin filtro de fecha, corre sobre todo el histórico.
    /// </summary>
    [HttpGet("kpis-piloto")]
    public async Task<ActionResult<KpisPilotoDto>> KpisPiloto(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, CancellationToken ct = default)
        => Ok(await kpisPiloto.Handle(new ObtenerKpisPilotoQuery(desde, hasta), ct));
}
