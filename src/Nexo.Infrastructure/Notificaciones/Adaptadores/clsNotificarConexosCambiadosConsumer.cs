using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Traduce el evento de conexos cambiados en la llamada al caso de uso que notifica. Misma regla que
/// <see cref="clsNotificarEntregaConsumer"/>: la excepción sube para que el bus reintente o mande a la cola de error.</summary>
public sealed class clsNotificarConexosCambiadosConsumer(
    ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto> notificar,
    ILogger<clsNotificarConexosCambiadosConsumer> log)
    : IConsumer<clsConexosCotizacionCambiados>
{
    public async Task Consume(ConsumeContext<clsConexosCotizacionCambiados> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(new clsNotificarConexosCambiadosCommand(m), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation(
                "Aviso de conexos {Accion} en la cotización {CotizacionId} encolado para {Destinatario} (id en el relay {IdEnElRelay}).",
                m.Accion, m.CotizacionId, r.Destinatario, r.IdEnElRelay ?? "sin id");
        else
            log.LogWarning(
                "El cambio de conexos de la cotización {CotizacionId} no se notificó: {Motivo}",
                m.CotizacionId, r.Motivo);
    }
}
