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

/// <summary>Bandeja Priorizada y ciclo de vida de la solicitud: alta, costeo, rechazo,
/// recálculo de score y carga masiva de líneas de licitación.</summary>
[ApiController]
[Authorize]
[Route("api/solicitudes")]
public class clsSolicitudesController(
    IQueryHandler<clsObtenerBandejaQuery, clsPaginaDto<clsBandejaItemDto>> bandeja,
    IQueryHandler<clsObtenerCosteoQuery, clsSolicitudCosteoDto> costeo,
    ICommandHandler<clsCrearSolicitudCommand, Guid> crear,
    ICommandHandler<clsCargarLineasBulkCommand, Guid> cargarLineas,
    ICommandHandler<clsRechazarSolicitudCommand, Unit> rechazar,
    ICommandHandler<clsRecalcularScoreCommand, clsDesgloseScoreDto> recalcular,
    ICommandHandler<clsResolverCatalogosDeLineasCommand, IReadOnlyList<clsCatalogoLineaResueltoDto>> resolverCatalogos)
    : ControllerBase
{
    /// <summary>
    /// Bandeja Priorizada (anti-FIFO). Filtros: estado de la solicitud, atención
    /// (pendientes | automatico | administrador | atendidas | todas) y rango de fecha
    /// de captura (desde/hasta). Sin filtros muestra la cola de pendientes.
    /// Pagina (page/pageSize, respuesta `{ items, total }`).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsPaginaDto<clsBandejaItemDto>>> GetBandeja(
        [FromQuery] string? estado, [FromQuery] string? atencion, [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta, [FromQuery] string? vigencia,
        [FromQuery] string orderBy = "score", // vestigial, como en el original: siempre ordena por score
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await bandeja.Handle(new clsObtenerBandejaQuery(new clsBandejaFiltro(estado, atencion, desde, hasta, vigencia), page, pageSize), ct));

    /// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsSolicitudCosteoDto>> GetCosteo(Guid id, CancellationToken ct = default)
        => Ok(await costeo.Handle(new clsObtenerCosteoQuery(id), ct));

    /// <summary>Ingresar una solicitud spot (crea la cabecera y su línea inicial).</summary>
    [HttpPost]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Crear([FromBody] clsCrearSolicitudRequest req, CancellationToken ct = default)
    {
        var linea = req.Linea is null ? null : new clsCrearLineaInput(req.Linea.RutaId, req.Linea.ConfigVehiculoId, req.Linea.Tipologia, req.Linea.Peso, req.Linea.ValorMercancia);
        var id = await crear.Handle(new clsCrearSolicitudCommand(req.ClienteId, req.ClienteBp, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado, req.ProbabilidadCierre, linea), ct);
        return Created($"/api/solicitudes/{id}", new { id });
    }

    /// <summary>
    /// Resuelve texto libre (origen/destino/tipo de vehículo) a RutaId/ConfigVehiculoId,
    /// creando lo que falte en el catálogo. NO crea ninguna solicitud: es el paso previo
    /// para que un formulario de captura pueda alimentar a lineas:bulk, que ya exige
    /// esos IDs resueltos y no los resuelve por su cuenta.
    /// </summary>
    [HttpPost("resolver-catalogos")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<clsCatalogoLineaResueltoDto>>> ResolverCatalogos(
        [FromBody] List<clsResolverCatalogoLineaRequest> req, CancellationToken ct = default)
    {
        var lineas = req.Select(lr => new clsResolverCatalogoLineaInput(lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria)).ToList();
        return Ok(await resolverCatalogos.Handle(new clsResolverCatalogosDeLineasCommand(lineas), ct));
    }

    /// <summary>
    /// Carga masiva de líneas de un pliego de licitación.
    ///
    /// Responde 202 con el identificador del trabajo, no con el resultado: las líneas
    /// quedan guardadas y el costeo se hace en segundo plano. El avance se consulta
    /// con ese identificador. Contrato en docs/contrato-integracion. 409 si la
    /// solicitud no está en un estado que admita más líneas.
    /// </summary>
    [HttpPost("{id:guid}/lineas:bulk")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CargarLineas(Guid id, [FromBody] clsCargarLineasBulkRequest req, CancellationToken ct = default)
    {
        var lineas = req.Lineas.Select(lr => new clsCrearLineaInput(
            lr.RutaId, lr.ConfigVehiculoId, lr.Tipologia, lr.Peso, lr.ValorMercancia, lr.HorasCargue, lr.HorasDescargue,
            lr.GrupoRutaId)).ToList();
        var trabajoId = await cargarLineas.Handle(new clsCargarLineasBulkCommand(id, lineas), ct);

        return Accepted(new
        {
            trabajoId,
            estado = "Pendiente",
            consultarEn = $"/api/trabajos/{trabajoId}",
        });
    }

    /// <summary>Rechazo del comercial: descarta la solicitud registrando el motivo. 409 si
    /// ya estaba rechazada/descartada.</summary>
    [HttpPost("{id:guid}/rechazar")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Rechazar(Guid id, [FromBody] clsRechazarSolicitudRequest req, CancellationToken ct = default)
    {
        await rechazar.Handle(new clsRechazarSolicitudCommand(id, req.Motivo, req.Comentario), ct);
        return NoContent();
    }

    /// <summary>Recalcula y persiste el score de prioridad. Devuelve el desglose por componente.</summary>
    [HttpPost("{id:guid}/recalcular-score")]
    [Authorize(Roles = "CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsDesgloseScoreDto>> Recalcular(Guid id, CancellationToken ct = default)
        => Ok(await recalcular.Handle(new clsRecalcularScoreCommand(id), ct));
}
