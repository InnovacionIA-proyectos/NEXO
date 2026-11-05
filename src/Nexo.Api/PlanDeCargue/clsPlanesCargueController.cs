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

/// <summary>Plan de cargue: orquestación async contra el Cubicador (202 + identificador, avance por SignalR en /hubs/plan-cargue, consulta de respaldo aquí). Ningún endpoint llama al Cubicador directo.</summary>
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
    IQueryHandler<clsObtenerEstadoCubicacionLineaQuery, clsEstadoCubicacionLineaDto> obtenerEstadoPorLinea,
    IQueryHandler<clsObtenerMetricasPlanQuery, clsMetricasCubicacionEvento?> obtenerMetricas,
    IQueryHandler<clsObtenerNoCargadasPlanQuery, IReadOnlyList<clsColocacionCargueDto>> obtenerNoCargadas,
    IQueryHandler<clsObtenerColocacionesPlanQuery, IReadOnlyList<clsColocacionCargueDto>> obtenerColocaciones)
    : ControllerBase
{
    /// <summary>
    /// Crea un plan de cargue. El PlanId lo genera NEXO; responde 202 porque la fila del Cubicador se crea async, mientras la fila espejo de NEXO queda creada ya, en estado "procesando".
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
    /// Escenario de demostración. 202 con un CorrelationId de seguimiento (aún no hay PlanId: el Cubicador elige el vehículo); el plan se sigue por /hubs/plan-cargue.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("demo")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<clsComandoAceptadoDto>> CrearDemo(CancellationToken ct = default)
        => Accepted(await crearPlanDemo.Handle(new clsCrearPlanDemoCommand(), ct));

    /// <summary>
    /// Crea la flota de un plan a partir de una línea ya cotizada (botón "Cubicar" de la Mesa de Costeo). 202 con un CorrelationId que es el propio <paramref name="lineaId"/>;
    /// se sondea con <c>GET .../por-linea/{lineaId}</c> hasta que aparezca la flota.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpPost("desde-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // PESO_NO_DECLARADO
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> CrearDesdeLinea(Guid lineaId, CancellationToken ct = default)
        => Accepted(await crearPlanDesdeLinea.Handle(new clsCrearPlanDesdeLineaCommand(lineaId), ct));

    /// <summary>
    /// Pide la optimización de un plan existente. Responde 202; el resultado llega por /hubs/plan-cargue o con GET /api/planes-cargue/{id}. 503 si la compuerta de paridad está cerrada.
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

    /// <summary>Borra el plan. Responde 202 (no 204): NEXO borra su fila espejo de inmediato, pero el borrado en el Cubicador es async.</summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminarPlan.Handle(new clsEliminarPlanCommand(id), ct));

    /// <summary>Ajuste manual de una colocación: 202 async, reconciliado por el aviso de rechazo en /hubs/plan-cargue si el Cubicador lo rechaza.</summary>
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

    // Lectura del visor 3D contra la proyección local: ningún endpoint llama al Cubicador. [Authorize] sin rol específico (cualquiera de los 6 roles puede consultar).

    /// <summary>Listado contra la proyección local, paginado (page/pageSize).</summary>
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
    /// Sondeo de la flota que crea <c>CrearDesdeLinea</c>, contra la proyección local. Responde 200 con lista vacía (nunca 404) mientras el comando sigue en proceso;
    /// ver <see cref="clsObtenerPlanesPorLineaQuery"/>.
    /// </summary>
    [Authorize]
    [HttpGet("por-linea/{lineaId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPlanFlotaItemDto>>> ObtenerPorLinea(Guid lineaId, CancellationToken ct = default)
        => Ok(await obtenerPlanesPorLinea.Handle(new clsObtenerPlanesPorLineaQuery(lineaId), ct));

    /// <summary>
    /// Dice si "crear plan desde línea" sigue <c>procesando</c>, quedó <c>listo</c> o fue <c>rechazado</c> por el Cubicador (con su código y mensaje, p. ej. <c>FLOTA_INSUFICIENTE</c>). Siempre 200.
    /// </summary>
    [Authorize]
    [HttpGet("por-linea/{lineaId:guid}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsEstadoCubicacionLineaDto>> ObtenerEstadoPorLinea(Guid lineaId, CancellationToken ct = default)
        => Ok(await obtenerEstadoPorLinea.Handle(new clsObtenerEstadoCubicacionLineaQuery(lineaId), ct));

    /// <summary>Detalle completo del plan (ítems, colocaciones con geometría, métricas, incidencias).</summary>
    [Authorize]
    [HttpGet("{id:guid}/resultado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsResultadoPlanDto>> ObtenerResultado(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new clsObtenerResultadoPlanQuery(id), ct));

    /// <summary>Los vehículos (planes) de la misma flota.</summary>
    [Authorize]
    [HttpGet("{id:guid}/flota")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPlanFlotaItemDto>>> ObtenerFlota(Guid id, CancellationToken ct = default)
        => Ok(await obtenerFlota.Handle(new clsObtenerFlotaPlanQuery(id), ct));

    /// <summary>Subconjunto de /resultado. 200 con <c>null</c> si el plan existe pero nunca se optimizó.</summary>
    [Authorize]
    [HttpGet("{id:guid}/metricas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsMetricasCubicacionEvento?>> ObtenerMetricas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerMetricas.Handle(new clsObtenerMetricasPlanQuery(id), ct));

    /// <summary>Subconjunto de /resultado: solo las colocaciones rechazadas.</summary>
    [Authorize]
    [HttpGet("{id:guid}/no-cargadas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<clsColocacionCargueDto>>> ObtenerNoCargadas(Guid id, CancellationToken ct = default)
        => Ok(await obtenerNoCargadas.Handle(new clsObtenerNoCargadasPlanQuery(id), ct));

    /// <summary>Todas las colocaciones del plan (cargadas y no), subconjunto de /resultado; ver <see cref="clsObtenerColocacionesPlanQuery"/>.</summary>
    [Authorize]
    [HttpGet("{id:guid}/colocaciones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<clsColocacionCargueDto>>> ObtenerColocaciones(Guid id, CancellationToken ct = default)
        => Ok(await obtenerColocaciones.Handle(new clsObtenerColocacionesPlanQuery(id), ct));

    /// <summary>Mismo contenido que /resultado; el nombre queda para una futura diferenciación (p. ej. un archivo).</summary>
    [Authorize]
    [HttpGet("{id:guid}/exportar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsResultadoPlanDto>> Exportar(Guid id, CancellationToken ct = default)
        => Ok(await obtenerResultado.Handle(new clsObtenerResultadoPlanQuery(id), ct));
}
