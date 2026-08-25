using CabinaFletes.Application.CasosDeUso.Costeo;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;

namespace CabinaFletes.Infrastructure.Costeo.Adaptadores;

/// <summary>
/// Traduce el mensaje de costeo en el comando del caso de uso, y nada más.
///
/// Un consumidor es un adaptador de entrada, igual que un controlador HTTP: recibe algo
/// del mundo exterior, lo convierte al lenguaje de la aplicación y delega. Si aquí
/// empezara a haber decisiones, la lógica quedaría atada al transporte.
///
/// NO captura excepciones a propósito. Un fallo de fuente tiene que subir para que
/// MassTransit reintente con su política escalonada; taparlo aquí convertiría un
/// problema transitorio en un pendiente definitivo.
/// </summary>
public sealed class CostearLineaConsumer(ICommandHandler<CostearLineaCommand, Unit> costear)
    : IConsumer<CostearLineaSolicitada>
{
    public Task Consume(ConsumeContext<CostearLineaSolicitada> contexto)
    {
        var m = contexto.Message;
        return costear.Handle(new CostearLineaCommand(m.TrabajoId, m.SolicitudId, m.LineaId), contexto.CancellationToken);
    }
}
