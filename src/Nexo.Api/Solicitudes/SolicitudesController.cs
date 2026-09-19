using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Solicitudes;

[ApiController]
[Authorize]
[Route("api/solicitudes")]
public class SolicitudesController(
    IQueryHandler<ObtenerBandejaQuery, PaginaDto<BandejaItemDto>> bandeja,
    IQueryHandler<ObtenerCosteoQuery, SolicitudCosteoDto> costeo,
    ICommandHandler<CrearSolicitudCommand, Guid> crear,
    ICommandHandler<CargarLineasBulkCommand, Guid> cargarLineas,
    ICommandHandler<RechazarSolicitudCommand, Unit> rechazar,
    ICommandHandler<RecalcularScoreCommand, DesgloseScoreDto> recalcular,
    ICommandHandler<ResolverCatalogosDeLineasCommand, IReadOnlyList<CatalogoLineaResueltoDto>> resolverCatalogos)
    : ControllerBase
{
    /// <summary>
    /// Bandeja Priorizada (anti-FIFO). Filtros: estado de la solicitud, atención
    /// (pendientes | automatico | administrador | atendidas | todas) y rango de fecha
    /// de captura (desde/hasta). Sin filtros muestra la cola de pendientes.
    /// Issue #166: pagina (page/pageSize, respuesta `{ items, total }`).
    /// </summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<PaginaDto<BandejaItemDto>>> GetBandeja(
        [FromQuery] string? estado, [FromQuery] string? atencion, [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta, [FromQuery] string? vigencia,
        [FromQuery] string orderBy = "score", // vestigial, como en el original: siempre ordena por score
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await bandeja.Handle(new ObtenerBandejaQuery(new BandejaFiltro(estado, atencion, desde, hasta, vigencia), page, pageSize), ct));

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

    /// <summary>
    /// Resuelve texto libre (origen/destino/tipo de vehículo) a RutaId/ConfigVehiculoId,
    /// creando lo que falte en el catálogo. NO crea ninguna solicitud: es el paso previo
    /// para que un formulario de captura pueda alimentar a lineas:bulk, que ya exige esos
    /// IDs resueltos y no los resuelve por su cuenta (issue #131).
    /// </summary>
    [HttpPost("resolver-catalogos")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    public async Task<ActionResult<IReadOnlyList<CatalogoLineaResueltoDto>>> ResolverCatalogos(
        [FromBody] List<ResolverCatalogoLineaRequest> req, CancellationToken ct = default)
    {
        var lineas = req.Select(lr => new ResolverCatalogoLineaInput(lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria)).ToList();
        return Ok(await resolverCatalogos.Handle(new ResolverCatalogosDeLineasCommand(lineas), ct));
    }

    /// <summary>
    /// Carga masiva de líneas de un pliego de licitación.
    ///
    /// Responde 202 con el identificador del trabajo, no con el resultado: las líneas
    /// quedan guardadas y el costeo se hace en segundo plano. El avance se consulta
    /// con ese identificador (issue #15). Contrato en docs/contrato-integracion.
    /// </summary>
    [HttpPost("{id:guid}/lineas:bulk")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> CargarLineas(Guid id, [FromBody] CargarLineasBulkRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new CrearLineaInput(lr.RutaId, lr.ConfigVehiculoId, lr.Tipologia, lr.Peso, lr.ValorMercancia)).ToList();
        var trabajoId = await cargarLineas.Handle(new CargarLineasBulkCommand(id, lineas), ct);

        return Accepted(new
        {
            trabajoId,
            estado = "Pendiente",
            consultarEn = $"/api/trabajos/{trabajoId}",
        });
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
