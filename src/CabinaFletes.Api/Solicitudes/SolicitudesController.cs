using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Solicitudes;

[ApiController]
[Authorize]
[Route("api/solicitudes")]
public class SolicitudesController(
    IQueryHandler<ObtenerBandejaQuery, IReadOnlyList<BandejaItemDto>> bandeja,
    IQueryHandler<ObtenerCosteoQuery, SolicitudCosteoDto> costeo,
    ICommandHandler<CrearSolicitudCommand, Guid> crear,
    ICommandHandler<CargarLineasBulkCommand, int> cargarLineas,
    ICommandHandler<RechazarSolicitudCommand, Unit> rechazar,
    ICommandHandler<RecalcularScoreCommand, DesgloseScoreDto> recalcular)
    : ControllerBase
{
    /// <summary>
    /// Bandeja Priorizada (anti-FIFO). Filtros: estado de la solicitud, atención
    /// (pendientes | automatico | administrador | atendidas | todas) y rango de fecha
    /// de captura (desde/hasta). Sin filtros muestra la cola de pendientes.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BandejaItemDto>>> GetBandeja(
        [FromQuery] string? estado, [FromQuery] string? atencion, [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta, [FromQuery] string? vigencia,
        [FromQuery] string orderBy = "score", // vestigial, como en el original: siempre ordena por score
        CancellationToken ct = default)
        => Ok(await bandeja.Handle(new ObtenerBandejaQuery(new BandejaFiltro(estado, atencion, desde, hasta, vigencia)), ct));

    /// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SolicitudCosteoDto>> GetCosteo(Guid id, CancellationToken ct = default)
        => Ok(await costeo.Handle(new ObtenerCosteoQuery(id), ct));

    /// <summary>Ingresar una solicitud spot (crea la cabecera y su línea inicial).</summary>
    [HttpPost]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    public async Task<ActionResult> Crear([FromBody] CrearSolicitudRequest req, CancellationToken ct = default)
    {
        var linea = req.Linea is null ? null : new CrearLineaInput(req.Linea.RutaId, req.Linea.ConfigVehiculoId, req.Linea.Tipologia, req.Linea.Peso, req.Linea.ValorMercancia);
        var id = await crear.Handle(new CrearSolicitudCommand(req.ClienteId, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado, req.ProbabilidadCierre, linea), ct);
        return Created($"/api/solicitudes/{id}", new { id });
    }

    /// <summary>Carga masiva de líneas de un pliego de licitación.</summary>
    [HttpPost("{id:guid}/lineas:bulk")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    public async Task<ActionResult> CargarLineas(Guid id, [FromBody] CargarLineasBulkRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new CrearLineaInput(lr.RutaId, lr.ConfigVehiculoId, lr.Tipologia, lr.Peso, lr.ValorMercancia)).ToList();
        var n = await cargarLineas.Handle(new CargarLineasBulkCommand(id, lineas), ct);
        return Ok(new { agregadas = n });
    }

    /// <summary>Rechazo del comercial: descarta la solicitud registrando el motivo.</summary>
    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    public async Task<ActionResult> Rechazar(Guid id, [FromBody] RechazarSolicitudRequest req, CancellationToken ct = default)
    {
        await rechazar.Handle(new RechazarSolicitudCommand(id, req.Motivo, req.Comentario), ct);
        return NoContent();
    }

    /// <summary>Recalcula y persiste el score de prioridad. Devuelve el desglose por componente.</summary>
    [HttpPost("{id:guid}/recalcular-score")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    public async Task<ActionResult<DesgloseScoreDto>> Recalcular(Guid id, CancellationToken ct = default)
        => Ok(await recalcular.Handle(new RecalcularScoreCommand(id), ct));
}
