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
// AdminBiblioteca no entra a la Bandeja, la Mesa ni las solicitudes (ni a leerlas). El [Authorize(Roles)] de clase se suma (AND) a los de cada método.
[Authorize(Roles = "Comercial,AdminTarifas,ServiciosLogisticos,LiderProceso,Admin")]
[Route("api/solicitudes")]
public class clsSolicitudesController(
    IQueryHandler<clsObtenerBandejaQuery, clsPaginaDto<clsBandejaItemDto>> bandeja,
    IQueryHandler<clsObtenerCosteoQuery, clsSolicitudCosteoDto> costeo,
    ICommandHandler<clsCrearSolicitudCommand, Guid> crear,
    ICommandHandler<clsCargarLineasBulkCommand, Guid> cargarLineas,
    ICommandHandler<clsRechazarSolicitudCommand, Unit> rechazar,
    ICommandHandler<clsRecalcularScoreCommand, clsDesgloseScoreDto> recalcular,
    ICommandHandler<clsResolverCatalogosDeLineasCommand, IReadOnlyList<clsCatalogoLineaResueltoDto>> resolverCatalogos,
    IQueryHandler<clsRecomendarVehiculoContenedorQuery, IReadOnlyList<clsRecomendacionVehiculoContenedorDto>> recomendarVehiculoContenedor)
    : ControllerBase
{
    /// <summary>
    /// Bandeja Priorizada (anti-FIFO), paginada (<c>{ items, total }</c>). Filtros: estado, atención (pendientes | automatico | administrador | atendidas | todas), fecha de captura,
    /// vigencia (vigente | vencida, contra el Deadline; sin Deadline cuenta como vigente) y resultado (esperando | ganada | perdida; otro valor → 400 RESULTADO_INVALIDO).
    /// Sin filtros muestra la cola de pendientes. Para Comercial la consulta se limita a lo que él creó.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsPaginaDto<clsBandejaItemDto>>> GetBandeja(
        [FromQuery] string? estado, [FromQuery] string? atencion, [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta, [FromQuery] string? vigencia, [FromQuery] string? resultado = null,
        [FromQuery] string orderBy = "score", // vestigial: siempre ordena por score
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await bandeja.Handle(new clsObtenerBandejaQuery(new clsBandejaFiltro(estado, atencion, desde, hasta, vigencia, resultado), page, pageSize), ct));

    /// <summary>Abrir solicitud: devuelve el costo base pre-armado (SICE-TAC + históricos) por línea.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsSolicitudCosteoDto>> GetCosteo(Guid id, CancellationToken ct = default)
        => Ok(await costeo.Handle(new clsObtenerCosteoQuery(id), ct));

    /// <summary>Ingresar una solicitud spot (crea la cabecera y su línea inicial).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Crear([FromBody] clsCrearSolicitudRequest req, CancellationToken ct = default)
    {
        var linea = req.Linea is null ? null : new clsCrearLineaInput(req.Linea.RutaId, req.Linea.ConfigVehiculoId, req.Linea.Tipologia, req.Linea.Peso, req.Linea.ValorMercancia);
        var id = await crear.Handle(new clsCrearSolicitudCommand(req.ClienteId, req.ClienteBp, req.Tipo, req.Modalidad, req.Deadline, req.ValorEstimado, req.ProbabilidadCierre, linea), ct);
        return Created($"/api/solicitudes/{id}", new { id });
    }

    /// <summary>Resuelve texto libre (origen/destino/vehículo) a RutaId/ConfigVehiculoId, creando lo que falte en el catálogo. No crea solicitud: es el paso previo para lineas:bulk, que exige esos IDs ya resueltos.</summary>
    [HttpPost("resolver-catalogos")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<clsCatalogoLineaResueltoDto>>> ResolverCatalogos(
        [FromBody] List<clsResolverCatalogoLineaRequest> req, CancellationToken ct = default)
    {
        var lineas = req.Select(lr => new clsResolverCatalogoLineaInput(lr.Origen, lr.Destino, lr.TipoVehiculo, lr.Carroceria)).ToList();
        return Ok(await resolverCatalogos.Handle(new clsResolverCatalogosDeLineasCommand(lineas), ct));
    }

    /// <summary>
    /// El vehículo que el costeo escogería para cada contenedor según su tamaño y peso (<c>tbl_rangos_peso_contenedor</c>), por lote y en el mismo orden.
    /// Solo lectura: el front lo prellena en la línea y lo manda como <c>tipoVehiculo</c> al crear la solicitud. <c>tipoVehiculo</c> va en null si el peso
    /// supera el tope del tamaño (<c>pesoMaximoContenedor</c> trae el tope más alto) o si la tabla no está disponible.
    /// </summary>
    [HttpPost("recomendar-vehiculo-contenedor")]
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<clsRecomendacionVehiculoContenedorDto>>> RecomendarVehiculoContenedor(
        [FromBody] List<clsRecomendarVehiculoContenedorRequest> req, CancellationToken ct = default)
        => Ok(await recomendarVehiculoContenedor.Handle(
            new clsRecomendarVehiculoContenedorQuery(req.Select(l => new clsRecomendacionVehiculoContenedorInput(l.Indice, l.ClaseContenedor, l.PesoKg)).ToList()), ct));

    /// <summary>Carga masiva de líneas de un pliego de licitación. Responde 202 con el identificador del trabajo: las líneas quedan guardadas y el costeo corre en segundo plano. 409 si la solicitud no admite más líneas.</summary>
    [HttpPost("{id:guid}/lineas:bulk")]
    [Authorize(Roles = "Admin")]
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
    [Authorize(Roles = "Comercial,Admin")]
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
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsDesgloseScoreDto>> Recalcular(Guid id, CancellationToken ct = default)
        => Ok(await recalcular.Handle(new clsRecalcularScoreCommand(id), ct));
}
