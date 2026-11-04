using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Costeo.Adaptadores;

/// <summary>Traduce el mensaje de costeo en el comando del caso de uso. No captura excepciones a propósito:
/// un fallo de fuente debe subir para que MassTransit reintente con su política escalonada.</summary>
public sealed class clsCostearLineaConsumer(ICommandHandler<clsCostearLineaCommand, Unit> costear)
    : IConsumer<clsCostearLineaSolicitada>
{
    public Task Consume(ConsumeContext<clsCostearLineaSolicitada> contexto)
    {
        var m = contexto.Message;
        return costear.Handle(new clsCostearLineaCommand(m.TrabajoId, m.SolicitudId, m.LineaId), contexto.CancellationToken);
    }
}
