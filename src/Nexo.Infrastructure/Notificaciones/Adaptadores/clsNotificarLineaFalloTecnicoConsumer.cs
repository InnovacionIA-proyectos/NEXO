using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Notificaciones.Adaptadores;

/// <summary>
/// Traduce una línea que agotó reintentos en el correo de alerta al Administrador de
/// Fletes (urgencia Media, sección 9 del documento de entendimiento). Consumidor
/// independiente del de la saga (clsAutoCosteoSaga también escucha este mismo mensaje):
/// MassTransit reparte el mismo evento a cada receive endpoint suscrito.
/// </summary>
public sealed class clsNotificarLineaFalloTecnicoConsumer(
    ICommandHandler<clsNotificarLineaFalloTecnicoCommand, clsResultadoDeNotificacionDto> notificar,
    ILogger<clsNotificarLineaFalloTecnicoConsumer> log)
    : IConsumer<clsLineaFalloTecnico>
{
    public async Task Consume(ConsumeContext<clsLineaFalloTecnico> contexto)
    {
        var m = contexto.Message;
        var r = await notificar.Handle(
            new clsNotificarLineaFalloTecnicoCommand(m.TrabajoId, m.LineaId, m.Motivo), contexto.CancellationToken);

        if (r.Enviado)
            log.LogInformation("Alerta de línea sin costear ({LineaId}) enviada a {Destinatarios}.", m.LineaId, r.Destinatario);
        else
            log.LogWarning("Línea sin costear ({LineaId}) sin notificar: {Motivo}", m.LineaId, r.Motivo);
    }
}
