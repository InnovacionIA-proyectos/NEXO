using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Priorizacion;

namespace Nexo.Api.Priorizacion;

/// <summary>
/// Publica un mensaje al arrancar y se va: no tiene reloj (vive en la saga, dentro del broker).
/// Se publica en cada arranque a propósito: es idempotente (la saga se correlaciona por una constante) y repara la cadena si el temporizador se perdió, p. ej. al borrar el volumen del broker.
/// Vive en Api porque <see cref="BackgroundService"/> es un tipo de hosting que Application no debe conocer.
/// </summary>
public sealed class clsArranqueDeLaCadenaHostedService(
    IServiceScopeFactory alcances,
    ILogger<clsArranqueDeLaCadenaHostedService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var alcance = alcances.CreateScope();
            var publicador = alcance.ServiceProvider.GetRequiredService<IPublicadorDeMensajes>();
            var uow = alcance.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await publicador.PublicarAsync(
                new clsArrancarPriorizacion(clsCadenaDePriorizacion.Unica), stoppingToken);

            // Hay que guardar: con el Outbox, publicar solo escribe el mensaje en la tabla de salida dentro de la transacción; sin SaveChanges la cadena no queda armada.
            await uow.SaveChangesAsync(stoppingToken);

            log.LogInformation(
                "Cadena de priorización armada. El reloj del recálculo vive en el broker, " +
                "no en este proceso.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado durante el arranque. No es un fallo.
        }
        catch (Exception ex)
        {
            // No se deja subir: que el API no arranque porque el broker no está listo sería peor que quedarse sin recálculo; el siguiente arranque reintenta (el mensaje es idempotente).
            log.LogError(ex,
                "No se pudo armar la cadena de priorización. El API sigue en pie; el recálculo " +
                "del score persistido no correrá hasta el próximo arranque o hasta que alguien " +
                "publique clsArrancarPriorizacion.");
        }
    }
}
