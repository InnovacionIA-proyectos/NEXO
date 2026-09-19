using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Mensajes.Costeo;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Trabajos.Adaptadores;

/// <summary>
/// Lee el estado actual del trabajo y lo empuja a quien lo esté mirando.
///
/// Lee en vez de confiar en lo que traiga el mensaje, y eso da dos cosas: el aviso sale
/// de la MISMA fuente que la consulta de respaldo —así no pueden decir cosas distintas—
/// y un mensaje entregado fuera de orden no puede hacer retroceder la barra de progreso.
///
/// UN FALLO AL AVISAR NO ES UN FALLO DEL TRABAJO. Si el aviso no sale, se registra y se
/// sigue: la verdad está en la consulta por identificador. Dejar que la excepción suba
/// mandaría el mensaje a reintentos y luego a la cola de error, disparando la alerta del
/// equipo porque un navegador se desconectó.
/// </summary>
public sealed class AvisarTrabajoConsumer(
    IEstadoDeTrabajoPort trabajos,
    INotificadorDeTrabajos notificador,
    ILogger<AvisarTrabajoConsumer> log)
    : IConsumer<TrabajoActualizado>
{
    public async Task Consume(ConsumeContext<TrabajoActualizado> contexto)
    {
        var trabajoId = contexto.Message.TrabajoId;

        try
        {
            var estado = await trabajos.ObtenerAsync(trabajoId, contexto.CancellationToken);
            if (estado is null) return;

            await notificador.AvisarAsync(AvisoDeTrabajo.Desde(estado), contexto.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el avance del trabajo {TrabajoId}. El trabajo sigue su curso; " +
                "el estado se puede consultar en /api/trabajos/{TrabajoId}.", trabajoId, trabajoId);
        }
    }
}
