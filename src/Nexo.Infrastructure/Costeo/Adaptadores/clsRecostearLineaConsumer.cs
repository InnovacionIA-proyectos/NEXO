using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>Adaptador de entrada del recosteo: traduce el mensaje al comando y delega.</summary>
public sealed class clsRecostearLineaConsumer(ICommandHandler<clsRecostearLineaCommand, Unit> recostear)
    : IConsumer<clsRecostearLineaSolicitada>
{
    public Task Consume(ConsumeContext<clsRecostearLineaSolicitada> contexto)
    {
        var m = contexto.Message;
        return recostear.Handle(new clsRecostearLineaCommand(m.TrabajoId, m.SolicitudId, m.LineaId), contexto.CancellationToken);
    }
}
