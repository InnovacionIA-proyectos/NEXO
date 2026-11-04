using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Mensajes.Costeo;
using MassTransit;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>Reparte un pliego en un mensaje de costeo por línea e informa a la saga el total autoritativo DESPUÉS de publicarlas:
/// si se cayera a mitad, la saga nunca recibiría un total y el timeout la cerraría en Parcial. Un pliego sin líneas informa cero y cierra completa.</summary>
public sealed class clsRepartidorDeLineasConsumer(ILineasDePliegoPort lineas)
    : IConsumer<clsRepartirLineasDeCosteo>
{
    public async Task Consume(ConsumeContext<clsRepartirLineasDeCosteo> contexto)
    {
        var mensaje = contexto.Message;
        var ids = await lineas.IdsPorSolicitudAsync(mensaje.SolicitudId, contexto.CancellationToken);

        foreach (var lineaId in ids)
            await contexto.Publish(new clsCostearLineaSolicitada(mensaje.TrabajoId, mensaje.SolicitudId, lineaId));

        await contexto.Publish(new clsPliegoRepartido(mensaje.TrabajoId, mensaje.SolicitudId, ids.Count));
    }
}
