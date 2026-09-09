namespace CabinaFletes.Mensajes.Cubicacion;

/// <summary>
/// Contrato de mensajes con el microservicio Cubicador (carpeta cubicador/ de este
/// mismo repo, Python + aio_pika — ver cubicador/CONTRATO.md sección 6).
///
/// Estos SÍ son mensajes reales de MassTransit (issue #28, S2-HU-075 — decisión del
/// equipo: MassTransit nativo, no un cliente RabbitMQ crudo del lado de NEXO):
/// `CubicacionSolicitada` se publica con `IPublishEndpoint`
/// (`Infrastructure/Cubicacion/CubicacionPublicadorAdapter.cs`) y
/// `PlanDeCargueGenerado`/`CubicacionRechazada` se consumen con `IConsumer&lt;T&gt;`
/// reales (`PlanDeCargueGeneradoConsumer`/`CubicacionRechazadaConsumer`) — heredan la
/// misma tubería de reintentos/Fault/alerta que el resto de NEXO.
///
/// Lo ÚNICO no estándar es la TOPOLOGÍA: Cubicador declara su propio exchange topic
/// (`cubicador.eventos`) con routing keys semánticas, no el exchange-por-tipo que
/// MassTransit crea por defecto — por eso `MensajeriaDependencyInjection.cs` anula esa
/// convención a mano solo para estos 3 tipos (`cfg.Message&lt;T&gt;()`/
/// `cfg.Publish&lt;T&gt;()`/`ReceiveEndpoint` con `Bind()` manual). Cubicador, del otro
/// lado, no adopta MassTransit — solo aprendió a hablar su "sobre" JSON mínimo en estos
/// 2 puntos de cruce (ver cubicador/infrastructure/mensajeria/rabbitmq.py y
/// cubicador/docs/DECISIONES_TECNICAS.md #12). Ver docs/mensajeria/CUBICACION.md para
/// el detalle completo.
/// </summary>

/// <summary>
/// Comando saliente: NEXO -> Cubicador, routing key `cubicacion.solicitada`.
/// Mismo shape que espera `worker/main.py::_ejecutar_solicitud`.
///
/// `Solicitud` es <c>object</c> a propósito (S3-HU-02, issue #103): los 9 tipos que hoy
/// viajan en este único sobre ("optimizar" + los 8 comandos de escritura del plan de
/// cargue) tienen cada uno su propio payload. Reutilizar EL MISMO tipo de mensaje en vez
/// de crear 9 records de MassTransit distintos es lo que permite que los 8 nuevos hereden
/// gratis la topología ya verificada en producción para "optimizar" (mismo exchange
/// `cubicador.eventos`, mismos reintentos 1s/5s/15s/60s, mismo DLQ, misma alerta —
/// configurados una sola vez, a nivel de bus, en MensajeriaDependencyInjection.cs) sin
/// tocar esa configuración. La validación de CUÁL payload le corresponde a cada "Tipo" la
/// hace <see cref="CubicacionSolicitadaValidador"/> con un patrón de tipo en tiempo de
/// ejecución — funciona porque la validación corre ANTES de serializar, sobre la misma
/// instancia en memoria con la que se construyó el mensaje.
/// </summary>
public sealed record CubicacionSolicitada(
    string Tipo, string PlanId, string CorrelationId, object Solicitud);

/// <summary>Payload de "optimizar" (S2-HU-08.1) y de "reoptimizar".</summary>
public sealed record SolicitudDeCubicacion(
    string Estrategia, string Algoritmo, int? Semilla, object? Ponderaciones, bool RespetarBloqueadas);

// ---------------------------------------------------------------------------------
// Payloads de los 8 comandos nuevos (S3-HU-02, issue #103). Cada uno espeja 1:1 el DTO
// de Cubicador citado en cubicador/CONTRATO.md §3.2 (mismos campos, PascalCase acá,
// camelCase en el JSON por JsonSerializerDefaults.Web — Cubicador los recibe con el
// mismo alias camelCase por `to_camel` en api/dtos/comun.py::ApiModel). Ver
// docs/mensajeria/CUBICACION.md y el contrato publicado en la issue #108 para el
// detalle completo de cada endpoint.
// ---------------------------------------------------------------------------------

/// <summary>"crear_plan" — espeja CrearPlanRequest. El plan lo crea Cubicador con el
/// PlanId que ya viene en el sobre (nunca uno propio) — ver CrearPlanCommand.</summary>
public sealed record CrearPlanInput(string Nombre, string VehiculoId);

/// <summary>"crear_plan_demo" — sin payload: Cubicador elige internamente el vehículo y
/// los ítems del escenario de demostración (equivalente a application/plan_cargue/demo.py).</summary>
public sealed record CrearPlanDemoInput;

public sealed record EjeVehiculoInput(decimal PosicionMm, decimal CapacidadKg);

/// <summary>"guardar_vehiculo" — espeja GuardarVehiculoInput. Id nulo = alta; con Id =
/// actualización (upsert), igual semántica que el endpoint síncrono que reemplaza.</summary>
public sealed record GuardarVehiculoInput(
    string? Id,
    string Nombre,
    string? Descripcion,
    decimal LargoInternoMm,
    decimal AnchoInternoMm,
    decimal AltoInternoMm,
    decimal CapacidadKg,
    decimal TaraKg,
    decimal PuertaAnchoMm,
    decimal PuertaAltoMm,
    string PuertaPosicion,
    IReadOnlyList<EjeVehiculoInput> Ejes,
    decimal ToleranciaParedesMm = 0,
    decimal ToleranciaEntreUnidadesMm = 0);

/// <summary>"guardar_item" — espeja GuardarItemPlanCargueInput. Id nulo = alta; con Id =
/// actualización.</summary>
public sealed record GuardarItemInput(
    string? Id,
    string? Sku,
    string Descripcion,
    string? Pedido,
    string? Cliente,
    string? Destino,
    int SecuenciaEntrega,
    decimal LargoMm,
    decimal AnchoMm,
    decimal AltoMm,
    decimal PesoKg,
    int Cantidad,
    string? Color,
    int Prioridad,
    string? TipoEmbalaje,
    bool Apilable,
    int MaxNiveles,
    decimal MaxPesoEncimaKg,
    bool Rotable,
    bool MantenerVertical,
    bool Fragil,
    bool Peligroso,
    bool Refrigerado,
    string? GrupoCompatibilidad,
    string? Observaciones);

/// <summary>"importar_items" — entre 1 y 2.000 (misma regla que Cubicador valida otra vez
/// del lado del worker; NEXO valida temprano para dar 400 rápido sin encolar nada).</summary>
public sealed record ImportarItemsInput(IReadOnlyList<GuardarItemInput> Items);

/// <summary>"eliminar_item" — el PlanId del sobre identifica el plan; este payload solo
/// agrega el item dentro de ese plan.</summary>
public sealed record EliminarItemInput(string ItemId);

/// <summary>"eliminar_plan" — sin payload adicional: el PlanId del sobre basta.</summary>
public sealed record EliminarPlanInput;

/// <summary>"ajustar_colocacion" — espeja AjusteColocacionInput + el ColocacionId que el
/// PATCH síncrono llevaba en la ruta. Reemplaza el PATCH directo contra Cubicador
/// (CompuertaDeParidad.cs no aplica acá: es dato, no cálculo).</summary>
public sealed record AjustarColocacionInput(
    string ColocacionId,
    decimal? X,
    decimal? Y,
    decimal? Z,
    int? OrientacionIndice,
    bool? Bloqueada,
    bool? Retirar,
    bool? Reinsertar);

/// <summary>
/// Evento entrante: Cubicador -> NEXO, routing key `plan-de-cargue.generado`.
/// Mismo shape que publica `RabbitMqEventosPublisher.publicar_plan_generado`.
/// </summary>
public sealed record PlanDeCargueGenerado(string CorrelationId, string PlanId, ResultadoDeCubicacion Resultado);

public sealed record ResultadoDeCubicacion(string Estado, decimal Puntaje, int Colocaciones);

/// <summary>
/// Evento entrante: Cubicador -> NEXO, routing key `cubicacion.rechazada`.
/// Mismo shape que publica `RabbitMqEventosPublisher.publicar_cubicacion_rechazada`.
/// </summary>
public sealed record CubicacionRechazada(string CorrelationId, string PlanId, string Codigo, string Mensaje);
