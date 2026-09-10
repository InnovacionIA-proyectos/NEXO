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

/// <summary>"eliminar_vehiculo" — sin payload adicional: el id del vehículo viaja en el
/// PlanId del sobre, igual que hace "guardar_vehiculo" (ver la nota de esa asimetría en
/// <see cref="GuardarVehiculoInput"/>: los comandos de catálogo no tienen plan, así que
/// reutilizan ese campo como identidad del recurso afectado).
///
/// Cierra el par de "guardar_vehiculo": el frontend llama <c>eliminarVehiculo()</c>
/// (cubicador-api.service.ts) y sin este comando esa única escritura seguiría saliendo
/// del navegador directo a Cubicador con la API key en el bundle — lo que impide cerrar
/// S3-HU-05 (retirar el CORS y rotar la clave), que exige CERO llamadas de navegador.
///
/// Puede ser rechazado con VEHICULO_EN_USO: Cubicador se niega a borrar un vehículo con
/// planes de cargue asociados (application/plan_cargue/vehiculos.py::eliminar_vehiculo).
/// Es un rechazo de negocio normal, viaja como "cubicacion.rechazada" y no se
/// reintenta.</summary>
public sealed record EliminarVehiculoInput;

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
///
/// S3-HU-03 (issue #110/#111): enriquecido — antes <c>Resultado</c> solo traía
/// {estado, puntaje, colocaciones: int} (el CONTEO); ahora trae el <c>ResultadoPlanDto</c>
/// completo de Cubicador (items, colocaciones con geometría, métricas, incidencias) para
/// que <see cref="Infrastructure.Cubicacion.PlanDeCargueGeneradoConsumer"/> proyecte la
/// base de NEXO sin volver a consultar a Cubicador. Tamaño medido ANTES de este cambio
/// en docs/decisiones/medicion-evento-enriquecido-plan-cargue.md (issue #109).
/// </summary>
public sealed record PlanDeCargueGenerado(string CorrelationId, string PlanId, ResultadoDeCubicacion Resultado);

public sealed record ResultadoDeCubicacion(
    PlanCargueEvento Plan,
    VehiculoCubicacionEvento Vehiculo,
    IReadOnlyList<ColocacionCargueEvento> Colocaciones,
    MetricasCubicacionEvento? Metricas,
    IReadOnlyList<IncidenciaValidacionEvento> Incidencias);

public sealed record PlanCargueEvento(
    string Id, string Nombre, string VehiculoId, string? LineaSolicitudId, int NumeroVehiculo,
    string Estado, string Estrategia, string Algoritmo, int? Semilla,
    decimal PesoTotalKg, decimal VolumenUtilizadoM3, decimal UtilizacionVolumen, decimal UtilizacionPiso,
    decimal Puntaje, IReadOnlyList<ItemPlanCargueEvento> Items);

public sealed record ItemPlanCargueEvento(
    string Id, string PlanCargueId, string Sku, string Descripcion, string? Pedido, string? Cliente,
    string? Destino, int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg,
    int Cantidad, string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles,
    decimal MaxPesoEncimaKg, bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones);

public sealed record ColocacionCargueEvento(
    string Id, string PlanCargueId, string ItemPlanCargueId, int IndiceUnidad, bool Cargada,
    decimal X, decimal Y, decimal Z, decimal LargoColocadoMm, decimal AnchoColocadoMm, decimal AltoColocadoMm,
    int OrientacionIndice, int SecuenciaCargue, string? SoporteColocacionId, decimal PorcentajeSoporte,
    decimal PesoEncimaKg, string EstadoEstabilidad, string Accesibilidad, bool Bloqueada, bool AjusteManual,
    string? CodigoRechazo, string? RazonRechazo);

public sealed record MetricasCubicacionEvento(
    decimal VolumenInternoM3, decimal VolumenUtilizadoM3, decimal PorcentajeVolumen,
    decimal AreaPisoM2, decimal AreaPisoUtilizadaM2, decimal PorcentajePiso,
    decimal PesoMaximoKg, decimal PesoCargadoKg, decimal PorcentajePeso,
    int UnidadesTotales, int UnidadesCargadas, int UnidadesNoCargadas,
    int PedidosCompletos, int PedidosParciales, int Destinos, decimal AlturaMaximaMm,
    IReadOnlyList<decimal> CentroGravedadMm, IReadOnlyList<decimal> PesoPorEjeKg, decimal DiferenciaLadosKg,
    int Advertencias, int ViolacionesCriticas, decimal Puntaje,
    IReadOnlyDictionary<string, decimal> PuntajesParciales, decimal EspacioVacioUtilM3, decimal EspacioFragmentadoM3);

public sealed record IncidenciaValidacionEvento(
    string Severidad, string CodigoRegla, string Titulo, string Descripcion,
    string? AccionSugerida, string? ColocacionItemId);

/// <summary>
/// Evento entrante: Cubicador -> NEXO, routing key `cubicacion.rechazada`.
/// Mismo shape que publica `RabbitMqEventosPublisher.publicar_cubicacion_rechazada`.
/// </summary>
public sealed record CubicacionRechazada(string CorrelationId, string PlanId, string Codigo, string Mensaje);

// ---------------------------------------------------------------------------------
// S3-HU-03 (issue #110/#111): catálogo de vehículos. Cubicador es la fuente de verdad
// (ver ADR-planid-generado-por-nexo.md sobre por qué NEXO no la posee) — estos 2 eventos
// mantienen la proyección de NEXO (`VehiculosCubicacion`) al día para que
// GET /api/vehiculos (issue #112) nunca tenga que preguntarle a Cubicador en caliente.
// ---------------------------------------------------------------------------------

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.creado` — publicado
/// cuando `guardar_vehiculo` (S3-HU-02) crea una fila nueva (Id no existía antes).</summary>
public sealed record VehiculoCreado(string CorrelationId, VehiculoCubicacionEvento Vehiculo);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.actualizado` —
/// publicado cuando `guardar_vehiculo` actualiza una fila existente.</summary>
public sealed record VehiculoActualizado(string CorrelationId, VehiculoCubicacionEvento Vehiculo);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.eliminado` —
/// publicado cuando `eliminar_vehiculo` borra la fila de verdad (si Cubicador rechaza
/// con VEHICULO_EN_USO no se publica nada).
///
/// Cierra el trío del catálogo. Sin él la proyección de NEXO se quedaría con el vehículo
/// para siempre: <c>GET /api/vehiculos</c> lee de ahí, así que seguiría ofreciendo un
/// vehículo inexistente y el <c>crear_plan</c> que lo eligiera fallaría con
/// VEHICULO_INVALIDO.
///
/// Solo lleva el id, no el <see cref="VehiculoCubicacionEvento"/> completo: no hay nada
/// que proyectar, solo una fila que borrar.</summary>
public sealed record VehiculoEliminado(string CorrelationId, string VehiculoId);

public sealed record VehiculoCubicacionEvento(
    string Id, string Nombre, string? Descripcion,
    decimal LargoInternoMm, decimal AnchoInternoMm, decimal AltoInternoMm,
    decimal CapacidadKg, decimal TaraKg, decimal PuertaAnchoMm, decimal PuertaAltoMm, string PuertaPosicion,
    IReadOnlyList<EjeVehiculoInput> Ejes, decimal ToleranciaParedesMm, decimal ToleranciaEntreUnidadesMm,
    bool EsPreset);
