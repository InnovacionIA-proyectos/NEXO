using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Cubicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.PlanDeCargue;

public record SolicitarOptimizacionRequest(string? Estrategia, string? Algoritmo, int? Semilla, bool? RespetarBloqueadas);

public record CrearPlanRequest(string Nombre, Guid VehiculoId);

public record GuardarItemRequest(
    Guid? Id, string? Sku, string Descripcion, string? Pedido, string? Cliente, string? Destino,
    int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg, int Cantidad,
    string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles, decimal MaxPesoEncimaKg,
    bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones)
{
    public ItemAImportar AImportar() => new(
        Sku, Descripcion, Pedido, Cliente, Destino, SecuenciaEntrega, LargoMm, AnchoMm, AltoMm, PesoKg, Cantidad,
        Color, Prioridad, TipoEmbalaje, Apilable, MaxNiveles, MaxPesoEncimaKg, Rotable, MantenerVertical, Fragil,
        Peligroso, Refrigerado, GrupoCompatibilidad, Observaciones);
}

public record ImportarItemsRequest(IReadOnlyList<GuardarItemRequest> Items);

public record AjustarColocacionRequest(
    decimal? X, decimal? Y, decimal? Z, int? OrientacionIndice, bool? Bloqueada, bool? Retirar, bool? Reinsertar);

/// <summary>
/// Plan de cargue (S2-HU-08, extendido en S3-HU-02): orquestación async contra el motor
/// Cubicador. El patrón es el mismo que el resto de trabajos pesados del contrato —
/// 202 + identificador, avance por SignalR (/hubs/plan-cargue), consulta de respaldo acá
/// mismo. S3-HU-02 (issue #104) agrega la escritura del plan (alta, ítems, borrado,
/// ajuste manual) al mismo camino de cola: ninguno de estos endpoints toca Cubicador
/// directo, y su respuesta 202 se documenta caso por caso en la issue #108.
/// </summary>
[ApiController]
[Route("api/planes-cargue")]
public class PlanesCargueController(
    ICommandHandler<SolicitarOptimizacionCommand, SolicitudDeCubicacionAceptadaDto> solicitar,
    ICommandHandler<CrearPlanCommand, SolicitudDeCubicacionAceptadaDto> crearPlan,
    ICommandHandler<CrearPlanDemoCommand, ComandoAceptadoDto> crearPlanDemo,
    ICommandHandler<CrearPlanDesdeLineaCommand, ComandoAceptadoDto> crearPlanDesdeLinea,
    ICommandHandler<GuardarItemCommand, ComandoAceptadoDto> guardarItem,
    ICommandHandler<ImportarItemsCommand, ComandoAceptadoDto> importarItems,
    ICommandHandler<EliminarItemCommand, ComandoAceptadoDto> eliminarItem,
    ICommandHandler<EliminarPlanCommand, ComandoAceptadoDto> eliminarPlan,
    ICommandHandler<AjustarColocacionCommand, ComandoAceptadoDto> ajustarColocacion,
    IQueryHandler<ObtenerPlanCargueQuery, PlanCargueDto> consultar,
    IQueryHandler<ListarPlanesCargueQuery, IReadOnlyList<PlanCargueDto>> listar,
    IQueryHandler<ObtenerResultadoPlanQuery, ResultadoPlanDto> obtenerResultado,
    IQueryHandler<ObtenerFlotaPlanQuery, IReadOnlyList<PlanFlotaItemDto>> obtenerFlota,
    IQueryHandler<ObtenerPlanesPorLineaQuery, IReadOnlyList<PlanFlotaItemDto>> obtenerPlanesPorLinea,
    IQueryHandler<ObtenerMetricasPlanQuery, MetricasCubicacionEvento?> obtenerMetricas,
    IQueryHandler<ObtenerNoCargadasPlanQuery, IReadOnlyList<ColocacionCargueDto>> obtenerNoCargadas,
    IQueryHandler<ObtenerColocacionesPlanQuery, IReadOnlyList<ColocacionCargueDto>> obtenerColocaciones)
    : ControllerBase
{
    /// <summary>
    /// S3-HU-02 (issue #104): crea un plan de cargue nuevo. El PlanId lo genera NEXO acá
    /// (S3-HU-01.2) — responde 202 porque la fila de Cubicador se crea async, aunque la
    /// fila espejo de NEXO (consultable de inmediato con el GET de abajo) queda creada en
    /// el mismo momento de este POST, en estado "procesando" hasta que llegue la
    /// confirmación de Cubicador (ver <see cref="CrearPlanCommand"/> para la carrera que
    /// ese estado evita).
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SolicitudDeCubicacionAceptadaDto>> Crear(
        [FromBody] CrearPlanRequest body, CancellationToken ct = default)
    {
        var r = await crearPlan.Handle(new CrearPlanCommand(body.Nombre, body.VehiculoId), ct);
        return Accepted(r.ConsultarEn, r);
    }

    /// <summary>
    /// S3-HU-02 (issue #104): escenario de demostración. 202 con un CorrelationId de
    /// seguimiento — a diferencia de <see cref="Crear"/>, acá NO hay un PlanId de NEXO
    /// todavía (Cubicador elige el vehículo internamente): el plan resultante se sigue
    /// por /hubs/plan-cargue una vez Cubicador lo confirma (ver limitación documentada
    /// en la issue #108).
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost("demo")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ComandoAceptadoDto>> CrearDemo(CancellationToken ct = default)
        => Accepted(await crearPlanDemo.Handle(new CrearPlanDemoCommand(), ct));

    /// <summary>
    /// [S3-HU-04.3, issue #78] Crea la flota de un plan de cargue a partir de una línea de
    /// solicitud ya cotizada (botón "Cubicar" de la Mesa de Costeo) — reemplaza el POST que
    /// <c>crearPlanDesdeLinea()</c> (cubicador-api.service.ts) hacía directo contra
    /// Cubicador con la clave compartida. 202 con un CorrelationId de seguimiento que ES el
    /// propio <paramref name="lineaId"/> (no hay PlanId de NEXO todavía: Cubicador decide
    /// cuántos vehículos hacen falta) — sondear con <c>GET .../por-linea/{lineaId}</c>
    /// (<see cref="ObtenerPlanesPorLineaQuery"/>) hasta que aparezca la flota.
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost("desde-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> CrearDesdeLinea(Guid lineaId, CancellationToken ct = default)
        => Accepted(await crearPlanDesdeLinea.Handle(new CrearPlanDesdeLineaCommand(lineaId), ct));

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

    /// <summary>S3-HU-02 (issue #104): alta/edición de un ítem del plan (Id nulo = alta).</summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> GuardarItem(
        Guid id, [FromBody] GuardarItemRequest body, CancellationToken ct = default)
    {
        var r = await guardarItem.Handle(new GuardarItemCommand(
            id, body.Id, body.Sku, body.Descripcion, body.Pedido, body.Cliente, body.Destino, body.SecuenciaEntrega,
            body.LargoMm, body.AnchoMm, body.AltoMm, body.PesoKg, body.Cantidad, body.Color, body.Prioridad,
            body.TipoEmbalaje, body.Apilable, body.MaxNiveles, body.MaxPesoEncimaKg, body.Rotable,
            body.MantenerVertical, body.Fragil, body.Peligroso, body.Refrigerado, body.GrupoCompatibilidad,
            body.Observaciones), ct);
        return Accepted(r);
    }

    /// <summary>S3-HU-02 (issue #104): importación masiva de ítems (1-2.000).</summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost("{id:guid}/items/importar")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> ImportarItems(
        Guid id, [FromBody] ImportarItemsRequest body, CancellationToken ct = default)
    {
        var r = await importarItems.Handle(
            new ImportarItemsCommand(id, body.Items.Select(i => i.AImportar()).ToList()), ct);
        return Accepted(r);
    }

    /// <summary>S3-HU-02 (issue #104): borra un ítem del plan.</summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> EliminarItem(
        Guid id, Guid itemId, CancellationToken ct = default)
        => Accepted(await eliminarItem.Handle(new EliminarItemCommand(id, itemId), ct));

    /// <summary>
    /// S3-HU-02 (issue #104): borra el plan. Responde 202 (no 204): aunque NEXO ya borra
    /// su fila espejo antes de publicar (borrado optimista), el borrado en Cubicador sigue
    /// siendo async — 202 documenta esa asimetría en vez de prometer un 204 que no es
    /// del todo cierto (ver issue #108).
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminarPlan.Handle(new EliminarPlanCommand(id), ct));

    /// <summary>
    /// S3-HU-02 (issue #104, criterio 3): ajuste manual de una colocación. Deja de ser un
    /// PATCH síncrono contra Cubicador — 202 async, reconciliado por el aviso de rechazo
    /// en /hubs/plan-cargue si Cubicador lo rechaza.
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPatch("{id:guid}/colocaciones/{colocacionId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandoAceptadoDto>> AjustarColocacion(
        Guid id, Guid colocacionId, [FromBody] AjustarColocacionRequest body, CancellationToken ct = default)
    {
        var r = await ajustarColocacion.Handle(new AjustarColocacionCommand(
            id, colocacionId, body.X, body.Y, body.Z, body.OrientacionIndice, body.Bloqueada, body.Retirar,
            body.Reinsertar), ct);
        return Accepted(r);
    }

    // ----------------------------------------------------------------------------
    // S3-HU-03 (issue #112): lectura del visor 3D, toda contra la proyección local que
    // mantienen PlanDeCargueGeneradoConsumer/VehiculoCatalogoConsumer (issue #111) —
    // NINGUNO de estos endpoints llama a Cubicador (criterio no-negociable de la HU).
    // [Authorize] sin rol específico: mismo criterio que el resto de las consultas del
    // sistema (docs/ROLES_Y_PERMISOS.md — "cualquiera de los 6 roles puede consultar").
    // ----------------------------------------------------------------------------

    /// <summary>GET /api/planes-cargue — listado (100 más recientes) contra la proyección local.</summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanCargueDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new ListarPlanesCargueQuery(), ct));

    /// <summary>
    /// [S3-HU-04.3, issue #78] GET /api/planes-cargue/por-linea/{lineaId} — sondeo de la
    /// flota que crea <c>CrearDesdeLinea</c>, contra la proyección local. Responde
    /// 200 con lista VACÍA (nunca 404) mientras el comando sigue en proceso — ver
    /// <see cref="ObtenerPlanesPorLineaQuery"/>. Ubicado antes del GET por id en este
    /// archivo solo por legibilidad: el ruteo no depende del orden, "por-linea" no calza
    /// con la restricción <c>:guid</c> de <c>{id:guid}</c>.
    /// </summary>
    [Authorize]
    [HttpGet("por-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanFlotaItemDto>>> ObtenerPorLinea(Guid lineaId, CancellationToken ct = default)
        => Ok(await obtenerPlanesPorLinea.Handle(new ObtenerPlanesPorLineaQuery(lineaId), ct));

    /// <summary>GET /api/planes-cargue/{id}/resultado — detalle completo (ítems,
    /// colocaciones con geometría, métricas, incidencias).</summary>
    [Authorize]
    [HttpGet("{id:guid}/resultado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoPlanDto>> ObtenerResultado(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new ObtenerResultadoPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/flota — los vehículos (planes) de la misma flota.</summary>
    [Authorize]
    [HttpGet("{id:guid}/flota")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanFlotaItemDto>>> ObtenerFlota(Guid id, CancellationToken ct = default)
        => Ok(await obtenerFlota.Handle(new ObtenerFlotaPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/metricas — subconjunto de /resultado.
    /// Responde 200 con <c>null</c> si el plan existe pero nunca se optimizó.</summary>
    [Authorize]
    [HttpGet("{id:guid}/metricas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetricasCubicacionEvento?>> ObtenerMetricas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerMetricas.Handle(new ObtenerMetricasPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/no-cargadas — subconjunto de /resultado, solo
    /// las colocaciones rechazadas.</summary>
    [Authorize]
    [HttpGet("{id:guid}/no-cargadas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ColocacionCargueDto>>> ObtenerNoCargadas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerNoCargadas.Handle(new ObtenerNoCargadasPlanQuery(id), ct));

    /// <summary>
    /// [S3-HU-04.3, issue #78] GET /api/planes-cargue/{id}/colocaciones — todas las
    /// colocaciones del plan (cargadas y no), subconjunto de /resultado. Reemplazo 1:1 de
    /// <c>colocaciones()</c> en cubicador-api.service.ts — ver
    /// <see cref="ObtenerColocacionesPlanQuery"/> para por qué se implementa en vez de
    /// deprecarlo a favor de /resultado.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/colocaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ColocacionCargueDto>>> ObtenerColocaciones(Guid id, CancellationToken ct = default)
        => Ok(await obtenerColocaciones.Handle(new ObtenerColocacionesPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/exportar — mismo shape que /resultado (igual
    /// que Cubicador: hoy es el mismo contenido, el nombre queda para una futura
    /// diferenciación real, p.ej. un archivo).</summary>
    [Authorize]
    [HttpGet("{id:guid}/exportar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultadoPlanDto>> Exportar(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new ObtenerResultadoPlanQuery(id), ct));
}
