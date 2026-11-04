namespace Nexo.Mensajes.Cubicacion;

/// <summary>
/// Comando saliente: NEXO -> Cubicador, routing key `cubicacion.solicitada`.
///
/// `Solicitud` es <c>object</c> a propósito: todos los tipos de comando comparten este único sobre
/// (misma topología, reintentos y DLQ). <see cref="clsCubicacionSolicitadaValidador"/> valida el payload
/// de cada "Tipo" en tiempo de ejecución, antes de serializar.
/// </summary>
public sealed record clsCubicacionSolicitada(
    string Tipo, string PlanId, string CorrelationId, object Solicitud);

/// <summary>Payload de "optimizar" y de "reoptimizar".</summary>
public sealed record clsSolicitudDeCubicacion(
    string Estrategia, string Algoritmo, int? Semilla, object? Ponderaciones, bool RespetarBloqueadas);

// Payloads de los comandos de escritura del plan de cargue: espejan 1:1 el DTO de Cubicador
// (mismos campos; PascalCase aquí, camelCase en el JSON).

/// <summary>"crear_plan": el plan lo crea Cubicador con el PlanId que ya viene en el sobre.</summary>
public sealed record clsCrearPlanInput(string Nombre, string VehiculoId);

/// <summary>"crear_plan_demo": sin payload; Cubicador elige el vehículo y los ítems del escenario de demostración.</summary>
public sealed record clsCrearPlanDemoInput;

/// <summary>"crear_desde_linea": genera la flota (uno o más vehículos) a partir de las posiciones de una línea de solicitud ya cotizada.</summary>
public sealed record clsCrearPlanDesdeLineaInput(string LineaSolicitudId);

public sealed record clsEjeVehiculoInput(decimal PosicionMm, decimal CapacidadKg);

/// <summary>"guardar_vehiculo": Id nulo = alta; con Id = actualización (upsert).</summary>
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

/// <summary>"guardar_item": Id nulo = alta; con Id = actualización.</summary>
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

/// <summary>"importar_items": entre 1 y 2.000; NEXO valida temprano para responder 400 sin encolar.</summary>
public sealed record clsImportarItemsInput(IReadOnlyList<clsGuardarItemInput> Items);

/// <summary>"eliminar_item": el PlanId del sobre identifica el plan; este payload solo agrega el item.</summary>
public sealed record clsEliminarItemInput(string ItemId);

/// <summary>"eliminar_plan": sin payload; el PlanId del sobre basta.</summary>
public sealed record clsEliminarPlanInput;

/// <summary>
/// "eliminar_vehiculo": sin payload; el id del vehículo viaja en el PlanId del sobre (los comandos de
/// catálogo no tienen plan). Cubicador puede rechazarlo con VEHICULO_EN_USO si hay planes asociados;
/// es un rechazo de negocio normal ("cubicacion.rechazada") y no se reintenta.
/// </summary>
public sealed record clsEliminarVehiculoInput;

/// <summary>"ajustar_colocacion": ajuste manual de una colocación; ColocacionId identifica cuál.</summary>
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
///
/// <c>Resultado</c> trae el resultado completo (items, colocaciones con geometría, métricas, incidencias)
/// para proyectar la base de NEXO sin volver a consultar a Cubicador.
/// Limitación: si "crear_desde_linea" reparte la carga en varios vehículos, solo se publica un evento,
/// para el primer plan de la flota.
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

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `cubicacion.rechazada`.</summary>
public sealed record CubicacionRechazada(string CorrelationId, string PlanId, string Codigo, string Mensaje);

// Catálogo de vehículos: Cubicador es la fuente de verdad; estos eventos mantienen al día la
// proyección de NEXO (`VehiculosCubicacion`).

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.creado`.</summary>
public sealed record VehiculoCreado(string CorrelationId, clsVehiculoCubicacionEvento Vehiculo);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `vehiculo.actualizado`.</summary>
public sealed record VehiculoActualizado(string CorrelationId, clsVehiculoCubicacionEvento Vehiculo);

/// <summary>
/// Evento entrante: Cubicador -> NEXO, routing key `vehiculo.eliminado`. Solo lleva el id: no hay nada
/// que proyectar, solo una fila que borrar. No se publica si Cubicador rechaza con VEHICULO_EN_USO.
/// </summary>
public sealed record VehiculoEliminado(string CorrelationId, string VehiculoId);

public sealed record clsVehiculoCubicacionEvento(
    string Id, string Nombre, string? Descripcion,
    decimal LargoInternoMm, decimal AnchoInternoMm, decimal AltoInternoMm,
    decimal CapacidadKg, decimal TaraKg, decimal PuertaAnchoMm, decimal PuertaAltoMm, string PuertaPosicion,
    IReadOnlyList<clsEjeVehiculoInput> Ejes, decimal ToleranciaParedesMm, decimal ToleranciaEntreUnidadesMm,
    bool EsPreset);

// Catálogos de tipo de mercancía y de carrocería: mismo criterio que el de vehículos. Creado/actualizado
// llevan el DTO completo; eliminado solo el id.

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `mercancia.creada`.</summary>
public sealed record MercanciaCreada(string CorrelationId, clsTipoMercanciaCatalogoEvento Mercancia);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `mercancia.actualizada`.</summary>
public sealed record MercanciaActualizada(string CorrelationId, clsTipoMercanciaCatalogoEvento Mercancia);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `mercancia.eliminada`. Solo el id.</summary>
public sealed record MercanciaEliminada(string CorrelationId, string MercanciaId);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `carroceria.creada`.</summary>
public sealed record CarroceriaCreada(string CorrelationId, clsTipoCarroceriaCatalogoEvento Carroceria);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `carroceria.actualizada`.</summary>
public sealed record CarroceriaActualizada(string CorrelationId, clsTipoCarroceriaCatalogoEvento Carroceria);

/// <summary>Evento entrante: Cubicador -> NEXO, routing key `carroceria.eliminada`. Solo el id.</summary>
public sealed record CarroceriaEliminada(string CorrelationId, string CarroceriaId);

/// <summary><see cref="CarroceriaPrincipal"/>/<see cref="CarroceriaSecundaria"/> llegan resueltas a CÓDIGO, no a id.</summary>
public sealed record clsTipoMercanciaCatalogoEvento(
    string Id, string CodigoMercancia, string NombreMercancia, string Categoria,
    bool Cubicable, bool CondicionadaCubicacion,
    string? CarroceriaPrincipal, string? CarroceriaSecundaria, bool CarroceriaSecundariaCondicionada,
    string ControlAmbientalRequerido, bool Apilable, bool Fragil, bool RotacionLibre,
    string? ComentarioCondicion, bool Activo, int Version);

public sealed record clsTipoCarroceriaCatalogoEvento(
    string Id, string CodigoCarroceria, string NombreCarroceria, bool Activo, int Version);
