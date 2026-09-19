using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>Adaptador de entrada del recosteo: traduce el mensaje al comando y delega.</summary>
public sealed class RecostearLineaConsumer(ICommandHandler<RecostearLineaCommand, Unit> recostear)
    : IConsumer<RecostearLineaSolicitada>
{
    public Task Consume(ConsumeContext<RecostearLineaSolicitada> contexto)
    {
        var m = contexto.Message;
        return recostear.Handle(new RecostearLineaCommand(m.TrabajoId, m.SolicitudId, m.LineaId), contexto.CancellationToken);
    }
}
