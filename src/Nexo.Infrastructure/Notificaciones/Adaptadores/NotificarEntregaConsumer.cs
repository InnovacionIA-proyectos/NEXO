using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Traduce el evento de entrega en la llamada al caso de uso que notifica.
///
/// Delgado a propósito: aquí no hay ninguna decisión. Quién recibe el correo, qué dice y
/// cuándo no hay a quién escribirle son preguntas de negocio y viven en Application.
///
/// LA EXCEPCIÓN SUBE. Es lo contrario de los consumidores de avisos (#17, #20), donde un
/// navegador desconectado no es una avería y el fallo se registra y se sigue. Aquí sí lo
/// es: una notificación perdida no se recupera consultando nada, no hay pantalla que la
/// muestre, y nadie se enteraría. Dejándola subir, el bus reintenta las fallas pasajeras
/// y manda a la cola de error las que no tienen arreglo — que es de donde sale la alerta
/// al equipo (#13).
/// </summary>
public sealed class NotificarEntregaConsumer(
    ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto> notificar,
    ILogger<NotificarEntregaConsumer> log)
    : IConsumer<CotizacionEntregada>
{
    public async Task Consume(ConsumeContext<CotizacionEntregada> contexto)
    {
        var m = contexto.Message;

        var r = await notificar.Handle(
            new NotificarEntregaCommand(m.CotizacionId, m.SolicitudId, m.FormatoPdf, m.NombrePdf),
            contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation(
                "Notificación de la cotización {CotizacionId} encolada para {Destinatario} " +
                "({Adjuntos} adjunto(s), id en el relay {IdEnElRelay}).",
                m.CotizacionId, r.Destinatario, r.Adjuntos, r.IdEnElRelay ?? "sin id");
        else
            // No es un fallo: es que no había correo que mandar. Se registra como aviso
            // para que se vea en el log sin confundirse con una avería.
            log.LogWarning(
                "La cotización {CotizacionId} no se notificó: {Motivo}",
                m.CotizacionId, r.Motivo);
    }
}
