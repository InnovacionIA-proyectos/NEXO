using CabinaFletes.Application.CasosDeUso.Costeo;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;

namespace CabinaFletes.Infrastructure.Costeo.Adaptadores;

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
