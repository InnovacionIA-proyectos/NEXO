using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Traduce una línea que agotó reintentos en el correo de alerta al Administrador de
/// Fletes (urgencia Media, sección 9 del documento de entendimiento). Consumidor
/// independiente del de la saga (AutoCosteoSaga también escucha este mismo mensaje):
/// MassTransit reparte el mismo evento a cada receive endpoint suscrito.
/// </summary>
public sealed class NotificarLineaFalloTecnicoConsumer(
    ICommandHandler<NotificarLineaFalloTecnicoCommand, ResultadoDeNotificacionDto> notificar,
    ILogger<NotificarLineaFalloTecnicoConsumer> log)
    : IConsumer<LineaFalloTecnico>
{
    public async Task Consume(ConsumeContext<LineaFalloTecnico> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(
            new NotificarLineaFalloTecnicoCommand(m.TrabajoId, m.LineaId, m.Motivo), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation("Alerta de línea sin costear ({LineaId}) enviada a {Destinatarios}.", m.LineaId, r.Destinatario);
        else
            log.LogWarning("Línea sin costear ({LineaId}) sin notificar: {Motivo}", m.LineaId, r.Motivo);
    }
}
