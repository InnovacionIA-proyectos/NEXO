namespace CabinaFletes.Mensajes.Cubicacion;

/// <summary>
/// Contrato de mensajes con el microservicio Cubicador (carpeta cubicador/ de este
/// mismo repo, Python + aio_pika — ver cubicador/docs/CONTRATOS.md sección B).
///
/// ADVERTENCIA para quien busque estos tipos en un consumer de MassTransit: no hay
/// ninguno. Cubicador publica JSON plano al exchange `cubicador.eventos` (topic), sin
/// el envelope de MassTransit (sin `messageType`, sin campo `message` anidado) — es
/// el mismo exchange/formato que ya usan sus propios tests, no algo que valga la pena
/// cambiar solo para que un `IConsumer&lt;T&gt;` nativo lo reciba. Por eso el puente
/// vive en Infrastructure/Cubicacion/ con RabbitMQ.Client directo (ver
/// CubicacionRabbitAdapter/CubicacionResultadosConsumidor), no con IPublishEndpoint/
/// IConsumer — igual que Cubicador tampoco usa "MassTransit para Python", conviven en
/// el mismo broker físico sin cruzarse (ver cubicador/docs/DECISIONES_TECNICAS.md #9).
///
/// Estos tipos son DTOs para (de)serializar en ese límite, con los mismos nombres de
/// campo (camelCase) que ya usa Cubicador — no participan del bus de MassTransit.
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
