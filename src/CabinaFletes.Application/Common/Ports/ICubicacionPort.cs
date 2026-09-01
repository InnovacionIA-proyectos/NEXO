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
/// usaría `IPublicadorDeMensajes`. Ver
/// CabinaFletes.Mensajes.Cubicacion.MensajesDeCubicacion y
/// docs/mensajeria/CUBICACION.md para el detalle completo.
///
/// Disciplina que exige el Outbox (no es una limitación de este puerto, es el patrón
/// que ya usa el resto de NEXO): el mensaje solo se entrega si el MISMO `DbContext`
/// llama a `SaveChangesAsync()` DESPUÉS de este método, no antes — confirmado en
/// runtime, ver docs/mensajeria/CUBICACION.md #5.
/// </summary>
public interface ICubicacionPort
{
    Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct);
}
