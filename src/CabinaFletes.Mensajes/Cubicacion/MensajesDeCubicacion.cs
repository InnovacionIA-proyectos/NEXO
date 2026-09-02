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
/// Mismo shape que espera `worker/main.py::_ejecutar_solicitud` para tipo="optimizar".
/// </summary>
public sealed record CubicacionSolicitada(
    string Tipo, string PlanId, string CorrelationId, SolicitudDeCubicacion Solicitud);

public sealed record SolicitudDeCubicacion(
    string Estrategia, string Algoritmo, int? Semilla, object? Ponderaciones, bool RespetarBloqueadas);

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
