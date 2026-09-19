using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Priorizacion;

namespace Nexo.Api.Priorizacion;

/// <summary>
/// Publica un mensaje al arrancar y se va. Eso es todo lo que hace.
///
/// Reemplaza a <c>RecalculoScoreHostedService</c>, que tenía un <c>PeriodicTimer</c>
/// adentro. La diferencia no es de estilo: aquí NO hay reloj. El reloj vive en el broker,
/// en la saga, y esto solo lo arma.
///
/// Se publica en cada arranque a propósito. Es idempotente —la saga se correlaciona por
/// una constante, así que un segundo arranque cae sobre la cadena que ya existe— y sirve
/// para repararla: si el temporizador se perdió, por ejemplo porque alguien borró el
/// volumen del broker, el siguiente arranque del API lo vuelve a armar. Sin esto la
/// cadena quedaría muerta en silencio y el síntoma tardaría semanas en aparecer, en
/// forma de reportes con scores viejos.
///
/// Vive en Api y no en Application por lo mismo que el anterior: <see cref="BackgroundService"/>
/// es un tipo de hosting, y Application no debe saber de eso.
/// </summary>
public sealed class ArranqueDeLaCadenaHostedService(
    IServiceScopeFactory alcances,
    ILogger<ArranqueDeLaCadenaHostedService> log)
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
                new ArrancarPriorizacion(CadenaDePriorizacion.Unica), stoppingToken);

            // HAY QUE GUARDAR. Con el Outbox, publicar no manda nada al broker: escribe el
            // mensaje en la tabla de salida dentro de la transacción, y sin SaveChanges esa
            // transacción no existe. Sin esta línea el arranque registraba en el log que la
            // cadena quedaba armada y no quedaba armada: cero filas en la tabla de la saga y
            // cero en la de salida. Se detectó levantando la aplicación de verdad, porque
            // ninguna prueba de mensajería usa el Outbox al publicar.
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
            // No se deja subir: que el API no arranque porque el broker todavía no está
            // listo sería peor que quedarse sin recálculo. Y el arranque siguiente lo
            // vuelve a intentar, porque este mensaje es idempotente por diseño.
            log.LogError(ex,
                "No se pudo armar la cadena de priorización. El API sigue en pie; el recálculo " +
                "del score persistido no correrá hasta el próximo arranque o hasta que alguien " +
                "publique ArrancarPriorizacion.");
        }
    }
}
