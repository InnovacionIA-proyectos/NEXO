namespace Nexo.Mensajes.Cubicacion;

/// <summary>
/// Contrato de mensajes con el microservicio Cubicador (carpeta cubicador/ de este
/// mismo repo, Python + aio_pika — ver cubicador/CONTRATO.md sección 6).
///
/// Son mensajes reales de MassTransit (MassTransit nativo, no un cliente RabbitMQ crudo
/// del lado de NEXO): `clsCubicacionSolicitada` se publica con `IPublishEndpoint`
/// (`Infrastructure/Cubicacion/clsCubicacionPublicadorAdapter.cs`) y
/// `clsPlanDeCargueGenerado`/`clsCubicacionRechazada` se consumen con `IConsumer&lt;T&gt;`
/// reales (`clsPlanDeCargueGeneradoConsumer`/`clsCubicacionRechazadaConsumer`) — heredan la
/// misma tubería de reintentos/Fault/alerta que el resto de NEXO.
///
/// Lo ÚNICO no estándar es la TOPOLOGÍA: Cubicador declara su propio exchange topic
/// (`cubicador.eventos`) con routing keys semánticas, no el exchange-por-tipo que
/// MassTransit crea por defecto — por eso `clsMensajeriaDependencyInjection.cs` anula esa
/// convención a mano solo para estos 3 tipos (`cfg.Message&lt;T&gt;()`/
/// `cfg.Publish&lt;T&gt;()`/`ReceiveEndpoint` con `Bind()` manual). Cubicador, del otro
/// lado, no adopta MassTransit — solo aprendió a hablar su "sobre" JSON mínimo en estos
/// 2 puntos de cruce (ver cubicador/infrastructure/mensajeria/rabbitmq.py y
/// cubicador/docs/DECISIONES_TECNICAS.md). Ver docs/mensajeria/CUBICACION.md para el
/// detalle completo.
/// </summary>

/// <summary>
/// Comando saliente: NEXO -> Cubicador, routing key `cubicacion.solicitada`.
/// Mismo shape que espera `worker/main.py::_ejecutar_solicitud`.
///
/// `clsSolicitud` es <c>object</c> a propósito: los 10 tipos que viajan en este único
/// sobre ("optimizar", "crear_desde_linea" y los demás comandos de escritura del plan de
/// cargue) tienen cada uno su propio payload. Reutilizar EL MISMO tipo de mensaje en vez
/// de crear un record de MassTransit por tipo es lo que permite que todos hereden la
/// misma topología (mismo exchange `cubicador.eventos`, mismos reintentos 1s/5s/15s/60s,
/// mismo DLQ, misma alerta — configurados una sola vez, a nivel de bus, en
/// clsMensajeriaDependencyInjection.cs) sin tocar esa configuración. La validación de CUÁL
/// payload le corresponde a cada "Tipo" la hace <see cref="clsCubicacionSolicitadaValidador"/>
/// con un patrón de tipo en tiempo de ejecución — funciona porque la validación corre
/// ANTES de serializar, sobre la misma instancia en memoria con la que se construyó el
/// mensaje.
/// </summary>
public sealed record clsCubicacionSolicitada(
    string Tipo, string PlanId, string CorrelationId, object Solicitud);

/// <summary>Payload de "optimizar" y de "reoptimizar".</summary>
public sealed record clsSolicitudDeCubicacion(
    string Estrategia, string Algoritmo, int? Semilla, object? Ponderaciones, bool RespetarBloqueadas);

// ---------------------------------------------------------------------------------
// Payloads de los comandos de escritura del plan de cargue. Cada uno espeja 1:1 el DTO
// de Cubicador citado en cubicador/CONTRATO.md §3.2 (mismos campos, PascalCase acá,
// camelCase en el JSON por JsonSerializerDefaults.Web — Cubicador los recibe con el
// mismo alias camelCase por `to_camel` en api/dtos/comun.py::ApiModel). Ver
// docs/mensajeria/CUBICACION.md para el detalle completo de cada endpoint.
// ---------------------------------------------------------------------------------

/// <summary>"crear_plan" — espeja clsCrearPlanRequest. El plan lo crea Cubicador con el
/// PlanId que ya viene en el sobre (nunca uno propio) — ver clsCrearPlanCommand.</summary>
public sealed record clsCrearPlanInput(string Nombre, string VehiculoId);

/// <summary>"crear_plan_demo" — sin payload: Cubicador elige internamente el vehículo y
/// los ítems del escenario de demostración (equivalente a application/plan_cargue/demo.py).</summary>
public sealed record clsCrearPlanDemoInput;

/// <summary>
/// "crear_desde_linea" — dispara la generación de la flota (uno o más vehículos, ver
/// <c>cubicador/application/plan_cargue/desde_linea.py</c>) a partir de las posiciones de
/// una línea de solicitud ya cotizada (botón "Cubicar" de la Mesa de Costeo).
/// <c>LineaSolicitudId</c> espeja el mismo campo (camelCase) que <c>worker/main.py</c> lee
/// como <c>solicitud["lineaSolicitudId"]</c>. Ver
/// <see cref="Application.CasosDeUso.PlanDeCargue.clsCrearPlanDesdeLineaCommand"/>.
/// </summary>
public sealed record clsCrearPlanDesdeLineaInput(string LineaSolicitudId);

public sealed record clsEjeVehiculoInput(decimal PosicionMm, decimal CapacidadKg);

/// <summary>"guardar_vehiculo" — espeja clsGuardarVehiculoInput. Id nulo = alta; con Id =
/// actualización (upsert), igual semántica que el endpoint síncrono que reemplaza.</summary>
public sealed record clsGuardarVehiculoInput(
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
    IReadOnlyList<clsEjeVehiculoInput> Ejes,
    decimal ToleranciaParedesMm = 0,
    decimal ToleranciaEntreUnidadesMm = 0);

/// <summary>"guardar_item" — espeja GuardarItemPlanCargueInput. Id nulo = alta; con Id =
/// actualización.</summary>
public sealed record clsGuardarItemInput(
    string? Id,
    string? Sku,
    string Descripcion,
    string? Pedido,
    string? clsCliente,
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
public sealed record clsImportarItemsInput(IReadOnlyList<clsGuardarItemInput> Items);

/// <summary>"eliminar_item" — el PlanId del sobre identifica el plan; este payload solo
/// agrega el item dentro de ese plan.</summary>
public sealed record clsEliminarItemInput(string ItemId);

/// <summary>"eliminar_plan" — sin payload adicional: el PlanId del sobre basta.</summary>
public sealed record clsEliminarPlanInput;

/// <summary>"eliminar_vehiculo" — sin payload adicional: el id del vehículo viaja en el
/// PlanId del sobre, igual que hace "guardar_vehiculo" (ver la nota de esa asimetría en
/// <see cref="clsGuardarVehiculoInput"/>: los comandos de catálogo no tienen plan, así que
/// reutilizan ese campo como identidad del recurso afectado).
///
/// Cierra el par de "guardar_vehiculo": el frontend llama <c>eliminarVehiculo()</c>
/// (cubicador-api.service.ts) y sin este comando esa única escritura seguiría saliendo
/// del navegador directo a Cubicador con la API key en el bundle, impidiendo retirar el
/// CORS y rotar la clave (cero llamadas de navegador a Cubicador).
///
/// Puede ser rechazado con VEHICULO_EN_USO: Cubicador se niega a borrar un vehículo con
/// planes de cargue asociados (application/plan_cargue/vehiculos.py::eliminar_vehiculo).
/// Es un rechazo de negocio normal, viaja como "cubicacion.rechazada" y no se
/// reintenta.</summary>
public sealed record clsEliminarVehiculoInput;

/// <summary>"ajustar_colocacion" — espeja AjusteColocacionInput + el ColocacionId que el
/// PATCH síncrono llevaba en la ruta. Reemplaza el PATCH directo contra Cubicador
/// (clsCompuertaDeParidad.cs no aplica acá: es dato, no cálculo).</summary>
public sealed record clsAjustarColocacionInput(
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
/// <c>Resultado</c> trae el <c>clsResultadoPlanDto</c> completo de Cubicador (items,
/// colocaciones con geometría, métricas, incidencias), no solo {estado, puntaje,
/// colocaciones: int}, para que
/// <see cref="Infrastructure.Cubicacion.clsPlanDeCargueGeneradoConsumer"/> proyecte la
/// base de NEXO sin volver a consultar a Cubicador. Tamaño medido en
/// docs/decisiones/medicion-evento-enriquecido-plan-cargue.md.
///
/// LIMITACIÓN CONOCIDA: cuando "crear_desde_linea" reparte la carga en varios vehículos,
/// hoy solo se publica UN evento de estos, para el primer plan de la flota (ver
/// worker/main.py::_ejecutar_solicitud) — los vehículos 2..N no llegan acá. Ver la nota
/// completa en <see cref="Application.CasosDeUso.PlanDeCargue.clsCrearPlanDesdeLineaCommand"/>.
/// </summary>
public sealed record PlanDeCargueGenerado(string CorrelationId, string PlanId, clsResultadoDeCubicacion Resultado);

public sealed record clsResultadoDeCubicacion(
    clsPlanCargueEvento Plan,
    clsVehiculoCubicacionEvento Vehiculo,
    IReadOnlyList<clsColocacionCargueEvento> Colocaciones,
    clsMetricasCubicacionEvento? Metricas,
    IReadOnlyList<clsIncidenciaValidacionEvento> Incidencias);

public sealed record clsPlanCargueEvento(
    string Id, string Nombre, string VehiculoId, string? LineaSolicitudId, int NumeroVehiculo,
    string Estado, string Estrategia, string Algoritmo, int? Semilla,
    decimal PesoTotalKg, decimal VolumenUtilizadoM3, decimal UtilizacionVolumen, decimal UtilizacionPiso,
    decimal Puntaje, IReadOnlyList<clsItemPlanCargueEvento> Items);

public sealed record clsItemPlanCargueEvento(
    string Id, string PlanCargueId, string Sku, string Descripcion, string? Pedido, string? clsCliente,
    string? Destino, int SecuenciaEntrega, decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg,
    int Cantidad, string? Color, int Prioridad, string? TipoEmbalaje, bool Apilable, int MaxNiveles,
    decimal MaxPesoEncimaKg, bool Rotable, bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado,
    string? GrupoCompatibilidad, string? Observaciones);

public sealed record clsColocacionCargueEvento(
    string Id, string PlanCargueId, string ItemPlanCargueId, int IndiceUnidad, bool Cargada,
    decimal X, decimal Y, decimal Z, decimal LargoColocadoMm, decimal AnchoColocadoMm, decimal AltoColocadoMm,
    int OrientacionIndice, int SecuenciaCargue, string? SoporteColocacionId, decimal PorcentajeSoporte,
    decimal PesoEncimaKg, string EstadoEstabilidad, string Accesibilidad, bool Bloqueada, bool AjusteManual,
    string? CodigoRechazo, string? RazonRechazo);

public sealed record clsMetricasCubicacionEvento(
    decimal VolumenInternoM3, decimal VolumenUtilizadoM3, decimal PorcentajeVolumen,
    decimal AreaPisoM2, decimal AreaPisoUtilizadaM2, decimal PorcentajePiso,
    decimal PesoMaximoKg, decimal PesoCargadoKg, decimal PorcentajePeso,
    int UnidadesTotales, int UnidadesCargadas, int UnidadesNoCargadas,
    int PedidosCompletos, int PedidosParciales, int Destinos, decimal AlturaMaximaMm,
    IReadOnlyList<decimal> CentroGravedadMm, IReadOnlyList<decimal> PesoPorEjeKg, decimal DiferenciaLadosKg,
    int Advertencias, int ViolacionesCriticas, decimal Puntaje,
    IReadOnlyDictionary<string, decimal> PuntajesParciales, decimal EspacioVacioUtilM3, decimal EspacioFragmentadoM3);

public sealed record clsIncidenciaValidacionEvento(
    string Severidad, string CodigoRegla, string Titulo, string Descripcion,
    string? AccionSugerida, string? ColocacionItemId);

/// <summary>
/// Evento entrante: Cubicador -> NEXO, routing key `cubicacion.rechazada`.
/// Mismo shape que publica `RabbitMqEventosPublisher.publicar_cubicacion_rechazada`.
/// </summary>
public sealed record CubicacionRechazada(string CorrelationId, string PlanId, string Codigo, string Mensaje);

// ---------------------------------------------------------------------------------
// Catálogo de vehículos. Cubicador es la fuente de verdad (ver
// ADR-planid-generado-por-nexo.md sobre por qué NEXO no la posee) — estos 2 eventos
// mantienen la proyección de NEXO (`VehiculosCubicacion`) al día para que
// GET /api/vehiculos nunca tenga que preguntarle a Cubicador en caliente.
// ---------------------------------------------------------------------------------

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.creado` — publicado
/// cuando `guardar_vehiculo` crea una fila nueva (Id no existía antes).</summary>
public sealed record VehiculoCreado(string CorrelationId, clsVehiculoCubicacionEvento Vehiculo);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.actualizado` —
/// publicado cuando `guardar_vehiculo` actualiza una fila existente.</summary>
public sealed record VehiculoActualizado(string CorrelationId, clsVehiculoCubicacionEvento Vehiculo);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.eliminado` —
/// publicado cuando `eliminar_vehiculo` borra la fila de verdad (si Cubicador rechaza
/// con VEHICULO_EN_USO no se publica nada).
///
/// Cierra el trío del catálogo. Sin él la proyección de NEXO se quedaría con el vehículo
/// para siempre: <c>GET /api/vehiculos</c> lee de ahí, así que seguiría ofreciendo un
/// vehículo inexistente y el <c>crear_plan</c> que lo eligiera fallaría con
/// VEHICULO_INVALIDO.
///
/// Solo lleva el id, no el <see cref="clsVehiculoCubicacionEvento"/> completo: no hay nada
/// que proyectar, solo una fila que borrar.</summary>
public sealed record VehiculoEliminado(string CorrelationId, string VehiculoId);

public sealed record clsVehiculoCubicacionEvento(
    string Id, string Nombre, string? Descripcion,
    decimal LargoInternoMm, decimal AnchoInternoMm, decimal AltoInternoMm,
    decimal CapacidadKg, decimal TaraKg, decimal PuertaAnchoMm, decimal PuertaAltoMm, string PuertaPosicion,
    IReadOnlyList<clsEjeVehiculoInput> Ejes, decimal ToleranciaParedesMm, decimal ToleranciaEntreUnidadesMm,
    bool EsPreset);
