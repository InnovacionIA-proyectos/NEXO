using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.PlanDeCargue;

public record SolicitarOptimizacionRequest(string? Estrategia, string? Algoritmo, int? Semilla, bool? RespetarBloqueadas);

/// <summary>
/// Plan de cargue (S2-HU-08): orquestación async contra el motor Cubicador. El patrón es
/// el mismo que el resto de trabajos pesados del contrato — 202 + identificador, avance
/// por SignalR (/hubs/plan-cargue), consulta de respaldo acá mismo.
/// </summary>
[ApiController]
[Route("api/planes-cargue")]
public class PlanesCargueController(
    ICommandHandler<SolicitarOptimizacionCommand, SolicitudDeCubicacionAceptadaDto> solicitar,
    IQueryHandler<ObtenerPlanCargueQuery, PlanCargueDto> consultar)
    : ControllerBase
{
    /// <summary>
    /// Pide la optimización de un plan de cargue ya existente. No bloquea: responde 202
    /// de inmediato, el resultado llega por /hubs/plan-cargue o se consulta con GET
    /// /api/planes-cargue/{id}. 503 si la compuerta de paridad (S2-HU-08.2) está cerrada.
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost("{id:guid}/optimizar")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SolicitudDeCubicacionAceptadaDto>> Optimizar(
        Guid id, [FromBody] SolicitarOptimizacionRequest? body, CancellationToken ct = default)
    {
        var r = await solicitar.Handle(
            new SolicitarOptimizacionCommand(
                id, body?.Estrategia, body?.Algoritmo, body?.Semilla, body?.RespetarBloqueadas),
            ct);
        return Accepted(r.ConsultarEn, r);
    }

    /// <summary>Consulta de respaldo: estado y resultado del plan, sin depender de haber
    /// recibido ningún aviso.</summary>
    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlanCargueDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await consultar.Handle(new ObtenerPlanCargueQuery(id), ct));
}
