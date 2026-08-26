using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;

namespace CabinaFletes.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Reparte un pliego en un mensaje de costeo por línea.
///
/// Existe para que la saga no tenga que leer la base. Ella pide repartir, este
/// consumidor lee los identificadores y publica uno por línea, y al final le informa el
/// total autoritativo — que es lo que la saga usa para saber cuándo está completa.
///
/// Se informa DESPUÉS de publicar las líneas, no antes: si se cayera a mitad del
/// reparto, la saga nunca recibiría un total y el timeout la cerraría en Parcial. Al
/// revés, la saga podría creerse completa con líneas que nunca se publicaron.
///
/// Un pliego sin líneas informa cero y la saga cierra completa de inmediato. Es
/// correcto: no hay nada que costear.
/// </summary>
public sealed class RepartidorDeLineasConsumer(ILineasDePliegoPort lineas)
    : IConsumer<RepartirLineasDeCosteo>
{
    public async Task Consume(ConsumeContext<RepartirLineasDeCosteo> contexto)
    {
        var mensaje = contexto.Message;
        var ids = await lineas.IdsPorSolicitudAsync(mensaje.SolicitudId, contexto.CancellationToken);

        foreach (var lineaId in ids)
            await contexto.Publish(new CostearLineaSolicitada(mensaje.TrabajoId, mensaje.SolicitudId, lineaId));

        await contexto.Publish(new PliegoRepartido(mensaje.TrabajoId, mensaje.SolicitudId, ids.Count));
    }
}
