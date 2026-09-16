using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Notificaciones;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

/// <summary>Traduce la tarifa vencida detectada en el correo de alerta (urgencia Alta, sección 9 del documento de entendimiento).</summary>
public sealed class NotificarTarifaVencidaConsumer(
    ICommandHandler<NotificarTarifaVencidaCommand, ResultadoDeNotificacionDto> notificar,
    ILogger<NotificarTarifaVencidaConsumer> log)
    : IConsumer<TarifaVencidaDetectada>
{
    public async Task Consume(ConsumeContext<TarifaVencidaDetectada> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(
            new NotificarTarifaVencidaCommand(m.LineaId, m.Justificacion, m.DetectadaEn), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation("Alerta de tarifa vencida (línea {LineaId}) enviada a {Destinatarios}.", m.LineaId, r.Destinatario);
        else
            log.LogWarning("Tarifa vencida en la línea {LineaId} sin notificar: {Motivo}", m.LineaId, r.Motivo);
    }
}
