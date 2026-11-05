using Nexo.Application.Common.Ports;
using MassTransit;

namespace Nexo.Infrastructure.Common.Mensajeria;

/// <summary>
/// Con el Outbox transaccional, publicar escribe una fila en la tabla de salida dentro de la transacción en curso:
/// si no confirma el mensaje no existe, y si confirma se entrega aunque RabbitMQ esté caído.
/// </summary>
public sealed class clsMassTransitPublicador(IPublishEndpoint publicador) : IPublicadorDeMensajes
{
    public Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class =>
        publicador.Publish(mensaje, ct);
}
