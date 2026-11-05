namespace Nexo.Application.Common.Ports;

/// <summary>
/// Publica a Cubicador con routing key propio (`cubicacion.solicitada` en `cubicador.eventos`), por eso no usa <see cref="IPublicadorDeMensajes"/>.
/// El Outbox solo entrega el mensaje si el mismo DbContext llama a SaveChangesAsync() DESPUÉS de publicar, también en PublicarComandoAsync.
/// </summary>
public interface ICubicacionPort
{
    Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct);

    /// <summary>
    /// Publica uno de los 8 comandos de escritura del plan de cargue por el mismo camino auditable que "optimizar".
    /// No consulta la paridad: son operaciones de datos, no de cálculo.
    /// </summary>
    /// <param name="tipo">Discriminador que usa el worker de Cubicador para elegir el caso de uso.</param>
    /// <param name="idCorrelacion">PlanId/CorrelationId del sobre; en comandos sin fila propia, un id de seguimiento.</param>
    Task PublicarComandoAsync(string tipo, string idCorrelacion, object solicitud, CancellationToken ct);
}
