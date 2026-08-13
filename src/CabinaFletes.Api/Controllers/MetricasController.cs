using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Metricas.Dtos;
using CabinaFletes.Application.Metricas.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

[ApiController]
[Authorize(Roles = "LiderProceso,AdminTarifas,Admin")]
[Route("api/metricas")]
public class MetricasController(IQueryHandler<ObtenerCumplimientoQuery, CumplimientoDto> cumplimiento)
    : ControllerBase
{
    /// <summary>KPIs de cumplimiento de SLA y estado de la cola (Tablero del Líder).</summary>
    [HttpGet("cumplimiento")]
    public async Task<ActionResult<CumplimientoDto>> Cumplimiento(CancellationToken ct = default)
        => Ok(await cumplimiento.Handle(new ObtenerCumplimientoQuery(), ct));
}
