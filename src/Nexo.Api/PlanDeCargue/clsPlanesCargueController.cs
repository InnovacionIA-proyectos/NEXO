using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Mensajes.Cubicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.PlanDeCargue;

public record clsSolicitarOptimizacionRequest(string? Estrategia, string? Algoritmo, int? Semilla, bool? RespetarBloqueadas);

public record clsCrearPlanRequest(string Nombre, Guid VehiculoId);

public record clsGuardarItemRequest(
    Guid? Id, string? Sku, string Descripcion, string? Pedido, string? clsCliente, string? Destino,
    int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg, int Cantidad,
    string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles, decimal MaxPesoEncimaKg,
    bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones)
{
    public clsItemAImportar AImportar() => new(
        Sku, Descripcion, Pedido, clsCliente, Destino, SecuenciaEntrega, LargoMm, AnchoMm, AltoMm, PesoKg, Cantidad,
        Color, Prioridad, TipoEmbalaje, Apilable, MaxNiveles, MaxPesoEncimaKg, Rotable, MantenerVertical, Fragil,
        Peligroso, Refrigerado, GrupoCompatibilidad, Observaciones);
}

public record clsImportarItemsRequest(IReadOnlyList<clsGuardarItemRequest> Items);

public record clsAjustarColocacionRequest(
    decimal? X, decimal? Y, decimal? Z, int? OrientacionIndice, bool? Bloqueada, bool? Retirar, bool? Reinsertar);

/// <summary>
/// Plan de cargue: orquestación async contra el motor Cubicador. El patrón es el mismo
/// que el resto de trabajos pesados del contrato — 202 + identificador, avance por
/// SignalR (/hubs/plan-cargue), consulta de respaldo acá mismo. La escritura del plan
/// (alta, ítems, borrado, ajuste manual) sigue el mismo camino de cola: ninguno de
/// estos endpoints toca Cubicador directo, y cada 202 documenta esa asimetría caso
/// por caso.
/// </summary>
[ApiController]
[Route("api/planes-cargue")]
public class clsPlanesCargueController(
    ICommandHandler<clsSolicitarOptimizacionCommand, clsSolicitudDeCubicacionAceptadaDto> solicitar,
    ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto> crearPlan,
    ICommandHandler<clsCrearPlanDemoCommand, clsComandoAceptadoDto> crearPlanDemo,
    ICommandHandler<clsCrearPlanDesdeLineaCommand, clsComandoAceptadoDto> crearPlanDesdeLinea,
    ICommandHandler<clsGuardarItemCommand, clsComandoAceptadoDto> guardarItem,
    ICommandHandler<clsImportarItemsCommand, clsComandoAceptadoDto> importarItems,
    ICommandHandler<clsEliminarItemCommand, clsComandoAceptadoDto> eliminarItem,
    ICommandHandler<clsEliminarPlanCommand, clsComandoAceptadoDto> eliminarPlan,
    ICommandHandler<clsAjustarColocacionCommand, clsComandoAceptadoDto> ajustarColocacion,
    IQueryHandler<clsObtenerPlanCargueQuery, clsPlanCargueDto> consultar,
    IQueryHandler<clsListarPlanesCargueQuery, clsPaginaDto<clsPlanCargueDto>> listar,
    IQueryHandler<clsObtenerResultadoPlanQuery, clsResultadoPlanDto> obtenerResultado,
    IQueryHandler<clsObtenerFlotaPlanQuery, IReadOnlyList<clsPlanFlotaItemDto>> obtenerFlota,
    IQueryHandler<clsObtenerPlanesPorLineaQuery, IReadOnlyList<clsPlanFlotaItemDto>> obtenerPlanesPorLinea,
    IQueryHandler<clsObtenerMetricasPlanQuery, clsMetricasCubicacionEvento?> obtenerMetricas,
    IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>> obtenerNoCargadas,
    IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>> obtenerColocaciones)
    : ControllerBase
{
    /// <summary>
    /// Crea un plan de cargue nuevo. El PlanId lo genera NEXO acá — responde 202 porque
    /// la fila de Cubicador se crea async, aunque la
    /// fila espejo de NEXO (consultable de inmediato con el GET de abajo) queda creada en
    /// el mismo momento de este POST, en estado "procesando" hasta que llegue la
    /// confirmación de Cubicador (ver <see cref="clsCrearPlanCommand"/> para la carrera que
    /// ese estado evita).
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsSolicitudDeCubicacionAceptadaDto>> Crear(
        [FromBody] clsCrearPlanRequest body, CancellationToken ct = default)
    {
        var r = await crearPlan.Handle(new clsCrearPlanCommand(body.Nombre, body.VehiculoId), ct);
        return Accepted(r.ConsultarEn, r);
    }

    /// <summary>
    /// Escenario de demostración. 202 con un CorrelationId de seguimiento — a diferencia
    /// de <see cref="Crear"/>, acá NO hay un PlanId de NEXO todavía (Cubicador elige el
    /// vehículo internamente): el plan resultante se sigue por /hubs/plan-cargue una vez
    /// Cubicador lo confirma.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("demo")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<clsComandoAceptadoDto>> CrearDemo(CancellationToken ct = default)
        => Accepted(await crearPlanDemo.Handle(new clsCrearPlanDemoCommand(), ct));

    /// <summary>
    /// Crea la flota de un plan de cargue a partir de una línea de solicitud ya cotizada
    /// (botón "Cubicar" de la Mesa de Costeo) — reemplaza el POST que
    /// <c>crearPlanDesdeLinea()</c> (cubicador-api.service.ts) hacía directo contra
    /// Cubicador con la clave compartida. 202 con un CorrelationId de seguimiento que ES el
    /// propio <paramref name="lineaId"/> (no hay PlanId de NEXO todavía: Cubicador decide
    /// cuántos vehículos hacen falta) — sondear con <c>GET .../por-linea/{lineaId}</c>
    /// (<see cref="clsObtenerPlanesPorLineaQuery"/>) hasta que aparezca la flota.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("desde-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> CrearDesdeLinea(Guid lineaId, CancellationToken ct = default)
        => Accepted(await crearPlanDesdeLinea.Handle(new clsCrearPlanDesdeLineaCommand(lineaId), ct));

    /// <summary>
    /// Pide la optimización de un plan de cargue ya existente. No bloquea: responde 202
    /// de inmediato, el resultado llega por /hubs/plan-cargue o se consulta con GET
    /// /api/planes-cargue/{id}. 503 si la compuerta de paridad está cerrada.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("{id:guid}/optimizar")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsSolicitudDeCubicacionAceptadaDto>> Optimizar(
        Guid id, [FromBody] clsSolicitarOptimizacionRequest? body, CancellationToken ct = default)
    {
        var r = await solicitar.Handle(
            new clsSolicitarOptimizacionCommand(
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
    public async Task<ActionResult<clsPlanCargueDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await consultar.Handle(new clsObtenerPlanCargueQuery(id), ct));

    /// <summary>Alta/edición de un ítem del plan (Id nulo = alta).</summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("{id:guid}/items")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> GuardarItem(
        Guid id, [FromBody] clsGuardarItemRequest body, CancellationToken ct = default)
    {
        var r = await guardarItem.Handle(new clsGuardarItemCommand(
            id, body.Id, body.Sku, body.Descripcion, body.Pedido, body.clsCliente, body.Destino, body.SecuenciaEntrega,
            body.LargoMm, body.AnchoMm, body.AltoMm, body.PesoKg, body.Cantidad, body.Color, body.Prioridad,
            body.TipoEmbalaje, body.Apilable, body.MaxNiveles, body.MaxPesoEncimaKg, body.Rotable,
            body.MantenerVertical, body.Fragil, body.Peligroso, body.Refrigerado, body.GrupoCompatibilidad,
            body.Observaciones), ct);
        return Accepted(r);
    }

    /// <summary>Importación masiva de ítems (1-2.000).</summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("{id:guid}/items/importar")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> ImportarItems(
        Guid id, [FromBody] clsImportarItemsRequest body, CancellationToken ct = default)
    {
        var r = await importarItems.Handle(
            new clsImportarItemsCommand(id, body.Items.Select(i => i.AImportar()).ToList()), ct);
        return Accepted(r);
    }

    /// <summary>Borra un ítem del plan.</summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> EliminarItem(
        Guid id, Guid itemId, CancellationToken ct = default)
        => Accepted(await eliminarItem.Handle(new clsEliminarItemCommand(id, itemId), ct));

    /// <summary>
    /// Borra el plan. Responde 202 (no 204): aunque NEXO ya borra su fila espejo antes
    /// de publicar (borrado optimista), el borrado en Cubicador sigue siendo async —
    /// 202 documenta esa asimetría en vez de prometer un 204 que no es del todo cierto.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminarPlan.Handle(new clsEliminarPlanCommand(id), ct));

    /// <summary>
    /// Ajuste manual de una colocación. Deja de ser un PATCH síncrono contra Cubicador
    /// — 202 async, reconciliado por el aviso de rechazo
    /// en /hubs/plan-cargue si Cubicador lo rechaza.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPatch("{id:guid}/colocaciones/{colocacionId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> AjustarColocacion(
        Guid id, Guid colocacionId, [FromBody] clsAjustarColocacionRequest body, CancellationToken ct = default)
    {
        var r = await ajustarColocacion.Handle(new clsAjustarColocacionCommand(
            id, colocacionId, body.X, body.Y, body.Z, body.OrientacionIndice, body.Bloqueada, body.Retirar,
            body.Reinsertar), ct);
        return Accepted(r);
    }

    // ----------------------------------------------------------------------------
    // Lectura del visor 3D, toda contra la proyección local que mantienen
    // clsPlanDeCargueGeneradoConsumer/clsVehiculoCatalogoConsumer — NINGUNO de estos
    // endpoints llama a Cubicador (criterio no-negociable). [Authorize] sin rol
    // específico: mismo criterio que el resto de las consultas del sistema
    // (docs/ROLES_Y_PERMISOS.md — "cualquiera de los 6 roles puede consultar").
    // ----------------------------------------------------------------------------

    /// <summary>GET /api/planes-cargue — listado contra la proyección local. Pagina
    /// (page/pageSize) en vez del tope fijo de 100 que tenía antes.</summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsPaginaDto<clsPlanCargueDto>>> Listar(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarPlanesCargueQuery(page, pageSize), ct));

    /// <summary>
    /// GET /api/planes-cargue/por-linea/{lineaId} — sondeo de la flota que crea
    /// <c>CrearDesdeLinea</c>, contra la proyección local. Responde
    /// 200 con lista VACÍA (nunca 404) mientras el comando sigue en proceso — ver
    /// <see cref="clsObtenerPlanesPorLineaQuery"/>. Ubicado antes del GET por id en este
    /// archivo solo por legibilidad: el ruteo no depende del orden, "por-linea" no calza
    /// con la restricción <c>:guid</c> de <c>{id:guid}</c>.
    /// </summary>
    [Authorize]
    [HttpGet("por-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPlanFlotaItemDto>>> ObtenerPorLinea(Guid lineaId, CancellationToken ct = default)
        => Ok(await obtenerPlanesPorLinea.Handle(new clsObtenerPlanesPorLineaQuery(lineaId), ct));

    /// <summary>GET /api/planes-cargue/{id}/resultado — detalle completo (ítems,
    /// colocaciones con geometría, métricas, incidencias).</summary>
    [Authorize]
    [HttpGet("{id:guid}/resultado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsResultadoPlanDto>> ObtenerResultado(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new clsObtenerResultadoPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/flota — los vehículos (planes) de la misma flota.</summary>
    [Authorize]
    [HttpGet("{id:guid}/flota")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPlanFlotaItemDto>>> ObtenerFlota(Guid id, CancellationToken ct = default)
        => Ok(await obtenerFlota.Handle(new clsObtenerFlotaPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/metricas — subconjunto de /resultado.
    /// Responde 200 con <c>null</c> si el plan existe pero nunca se optimizó.</summary>
    [Authorize]
    [HttpGet("{id:guid}/metricas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsMetricasCubicacionEvento?>> ObtenerMetricas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerMetricas.Handle(new clsObtenerMetricasPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/no-cargadas — subconjunto de /resultado, solo
    /// las colocaciones rechazadas.</summary>
    [Authorize]
    [HttpGet("{id:guid}/no-cargadas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<clsColocacionCargueDto>>> ObtenerNoCargadas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerNoCargadas.Handle(new clsObtenerNoCargadasPlanQuery(id), ct));

    /// <summary>
    /// GET /api/planes-cargue/{id}/colocaciones — todas las colocaciones del plan
    /// (cargadas y no), subconjunto de /resultado. Reemplazo 1:1 de
    /// <c>colocaciones()</c> en cubicador-api.service.ts — ver
    /// <see cref="clsObtenerColocacionesPlanQuery"/> para por qué se implementa en vez de
    /// deprecarlo a favor de /resultado.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/colocaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<clsColocacionCargueDto>>> ObtenerColocaciones(Guid id, CancellationToken ct = default)
        => Ok(await obtenerColocaciones.Handle(new clsObtenerColocacionesPlanQuery(id), ct));

    /// <summary>GET /api/planes-cargue/{id}/exportar — mismo shape que /resultado (igual
    /// que Cubicador: hoy es el mismo contenido, el nombre queda para una futura
    /// diferenciación real, p.ej. un archivo).</summary>
    [Authorize]
    [HttpGet("{id:guid}/exportar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsResultadoPlanDto>> Exportar(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new clsObtenerResultadoPlanQuery(id), ct));
}
