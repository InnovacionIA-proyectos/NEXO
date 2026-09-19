using CabinaFletes.Application.Common.Ports;
using MassTransit;

namespace CabinaFletes.Infrastructure.Common.Mensajeria;

/// <summary>
/// Implementa el puerto de publicación con MassTransit.
///
/// Parece un envoltorio trivial y lo es, pero esa es la idea: toda la mecánica está
/// en cómo se registra <see cref="IPublishEndpoint"/>. Con el Outbox transaccional
/// activo, publicar NO manda nada al broker: escribe una fila en la tabla de salida
/// dentro de la transacción en curso. El despachador la entrega después del commit.
///
/// De ahí las dos garantías que pide la issue: si la transacción no confirma el
/// mensaje no existe, y si confirma se entrega aunque RabbitMQ esté caído en ese
/// instante — se entregará cuando vuelva.
/// </summary>
public sealed class MassTransitPublicador(IPublishEndpoint publicador) : IPublicadorDeMensajes
{
    public Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class =>
        publicador.Publish(mensaje, ct);
}
