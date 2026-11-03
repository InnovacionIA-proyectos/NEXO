using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Traduce el evento de conexos cambiados tras la entrega en la llamada al caso de uso que
/// notifica (S5-HU-20.1/#286). Delgado a propósito, y con la misma regla que
/// <see cref="clsNotificarEntregaConsumer"/>: LA EXCEPCIÓN SUBE — un correo que no sale por
/// una falla de la salida corporativa no se recupera consultando nada, así que el bus
/// reintenta lo pasajero y manda a la cola de error lo que no tiene arreglo.
/// </summary>
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
