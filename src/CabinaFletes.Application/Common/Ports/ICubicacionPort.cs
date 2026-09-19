namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// Solicita una cubicación (plan de cargue) al microservicio Cubicador — issue #28,
/// S2-HU-075 (decisión del equipo: MassTransit nativo). NO usa
/// <see cref="IPublicadorDeMensajes"/> — ese puerto no expone forma de fijar el
/// routing key del mensaje, y Cubicador espera uno específico
/// (`cubicacion.solicitada`) en su propio exchange (`cubicador.eventos`), no el
/// exchange-por-tipo que MassTransit crea por defecto (confirmado en runtime: sin el
/// routing key correcto, el mensaje "se publica" pero se pierde en silencio). El
/// adaptador (`CubicacionPublicadorAdapter`) inyecta `IPublishEndpoint` directo con el
/// routing key fijado por mensaje — sigue recibiendo el Outbox transaccional de NEXO
/// (el Outbox intercepta a nivel de `IPublishEndpoint`, no de este puerto genérico) y
/// valida las invariantes a mano con el mismo <see cref="IValidadorDeMensaje{T}"/> que
/// usaría <see cref="IPublicadorDeMensajes"/>. Ver
/// CabinaFletes.Mensajes.Cubicacion.MensajesDeCubicacion y
/// docs/mensajeria/CUBICACION.md para el detalle completo.
///
/// Disciplina que exige el Outbox (no es una limitación de este puerto, es el patrón
/// que ya usa el resto de NEXO): el mensaje solo se entrega si el MISMO `DbContext`
/// llama a `SaveChangesAsync()` DESPUÉS de este método, no antes — confirmado en
/// runtime, ver docs/mensajeria/CUBICACION.md #5. Esto aplica IGUAL a
/// <see cref="PublicarComandoAsync"/>: aunque el comando no toque ninguna fila propia
/// de NEXO, el `SaveChangesAsync()` posterior sigue siendo obligatorio — es lo que
/// dispara el flush del outbox transaccional (`UseBusOutbox`), no la presencia de
/// cambios rastreados.
/// </summary>
public interface ICubicacionPort
{
    Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct);

    /// <summary>
    /// S3-HU-02 (issue #103/#104): publica cualquiera de los 8 comandos de escritura del
    /// plan de cargue (crear_plan, crear_plan_demo, guardar_vehiculo, guardar_item,
    /// importar_items, eliminar_item, eliminar_plan, ajustar_colocacion) por el MISMO
    /// camino auditable que "optimizar" — mismo exchange, mismos reintentos (1s/5s/15s/
    /// 60s), mismo DLQ y misma alerta por correo, todo configurado una sola vez a nivel
    /// de bus (ver MensajeriaDependencyInjection.cs). A propósito NO consulta
    /// <c>IParidadCubicadorPort</c>: son operaciones de datos, no de cálculo — la
    /// compuerta de paridad solo bloquea "optimizar" (ver HU S3-HU-02, criterio 5).
    /// </summary>
    /// <param name="tipo">Discriminador que el worker de Cubicador usa para elegir el caso
    /// de uso — uno de los 8 valores listados arriba.</param>
    /// <param name="idCorrelacion">PlanId/CorrelationId del sobre: la identidad con la que
    /// Cubicador crea o busca la fila afectada (o, para comandos sin fila propia como
    /// guardar_vehiculo/crear_plan_demo, un correlationId de seguimiento).</param>
    /// <param name="solicitud">Payload tipado según <paramref name="tipo"/> — ver
    /// MensajesDeCubicacion.cs para el shape exacto de cada uno.</param>
    Task PublicarComandoAsync(string tipo, string idCorrelacion, object solicitud, CancellationToken ct);
}
