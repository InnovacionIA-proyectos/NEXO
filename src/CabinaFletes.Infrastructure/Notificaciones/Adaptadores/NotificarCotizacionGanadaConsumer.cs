using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Traduce una cotización marcada como ganada en el correo de alerta al Administrador de Reglas (urgencia Baja, sección 9 del documento de entendimiento).</summary>
public sealed class NotificarCotizacionGanadaConsumer(
    ICommandHandler<NotificarCotizacionGanadaCommand, ResultadoDeNotificacionDto> notificar,
    ILogger<NotificarCotizacionGanadaConsumer> log)
    : IConsumer<CotizacionMarcadaGanada>
{
    public async Task Consume(ConsumeContext<CotizacionMarcadaGanada> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(
            new NotificarCotizacionGanadaCommand(m.CotizacionId, m.SolicitudId, m.MarcadaEn), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation("Alerta de cotización ganada ({CotizacionId}) enviada a {Destinatarios}.", m.CotizacionId, r.Destinatario);
        else
            log.LogWarning("Cotización ganada ({CotizacionId}) sin notificar: {Motivo}", m.CotizacionId, r.Motivo);
    }
}
