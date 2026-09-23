using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Traduce una cotización marcada como ganada en el correo de alerta al Administrador de Reglas (urgencia Baja, sección 9 del documento de entendimiento).</summary>
public sealed class clsNotificarCotizacionGanadaConsumer(
    ICommandHandler<clsNotificarCotizacionGanadaCommand, clsResultadoDeNotificacionDto> notificar,
    ILogger<clsNotificarCotizacionGanadaConsumer> log)
    : IConsumer<clsCotizacionMarcadaGanada>
{
    public async Task Consume(ConsumeContext<clsCotizacionMarcadaGanada> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(
            new clsNotificarCotizacionGanadaCommand(m.CotizacionId, m.SolicitudId, m.MarcadaEn), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation("Alerta de cotización ganada ({CotizacionId}) enviada a {Destinatarios}.", m.CotizacionId, r.Destinatario);
        else
            log.LogWarning("Cotización ganada ({CotizacionId}) sin notificar: {Motivo}", m.CotizacionId, r.Motivo);
    }
}
