using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Trabajos.Adaptadores;

/// <summary>Lee el estado actual del trabajo y lo empuja a quien lo mira. Lee en vez de confiar en el mensaje: aviso y consulta salen de
/// la misma fuente y un mensaje fuera de orden no hace retroceder el progreso. Un fallo al avisar se registra y se sigue.</summary>
public sealed class clsAvisarTrabajoConsumer(
    IEstadoDeTrabajoPort trabajos,
    INotificadorDeTrabajos notificador,
    ILogger<clsAvisarTrabajoConsumer> log)
    : IConsumer<clsTrabajoActualizado>
{
    public async Task Consume(ConsumeContext<clsTrabajoActualizado> contexto)
    {
        var trabajoId = contexto.Message.TrabajoId;

        try
        {
            var estado = await trabajos.ObtenerAsync(trabajoId, contexto.CancellationToken);
            if (estado is null) return;

            await notificador.AvisarAsync(clsAvisoDeTrabajo.Desde(estado), contexto.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el avance del trabajo {TrabajoId}. El trabajo sigue su curso; " +
                "el estado se puede consultar en /api/trabajos/{TrabajoId}.", trabajoId, trabajoId);
        }
    }
}
