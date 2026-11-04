using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Traduce el evento de entrega en la llamada al caso de uso que notifica (sin decisiones aquí). La excepción sube a propósito:
/// una notificación perdida no se recupera ni se ve en pantalla; el bus reintenta las pasajeras y manda a la cola de error las demás.</summary>
public sealed class clsNotificarEntregaConsumer(
    ICommandHandler<clsNotificarEntregaCommand, clsResultadoDeNotificacionDto> notificar,
    ILogger<clsNotificarEntregaConsumer> log)
    : IConsumer<clsCotizacionEntregada>
{
    public async Task Consume(ConsumeContext<clsCotizacionEntregada> contexto)
    {
        var m = contexto.Message;

        var r = await notificar.Handle(
            new clsNotificarEntregaCommand(m.CotizacionId, m.SolicitudId, m.FormatoPdf, m.NombrePdf),
            contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation(
                "Notificación de la cotización {CotizacionId} encolada para {Destinatario} " +
                "({Adjuntos} adjunto(s), id en el relay {IdEnElRelay}).",
                m.CotizacionId, r.Destinatario, r.Adjuntos, r.IdEnElRelay ?? "sin id");
        else
            // No es un fallo: no había correo que mandar; se registra como aviso.
            log.LogWarning(
                "La cotización {CotizacionId} no se notificó: {Motivo}",
                m.CotizacionId, r.Motivo);
    }
}
