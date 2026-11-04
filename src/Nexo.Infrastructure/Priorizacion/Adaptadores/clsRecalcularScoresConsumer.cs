using Nexo.Application.CasosDeUso.Priorizacion;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Infrastructure.Common.Mensajeria;
using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Priorizacion;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Priorizacion.Adaptadores;

/// <summary>Hace el recálculo que la saga pidió. La excepción sube a propósito: si el score persistido deja de refrescarse,
/// los reportes mienten sin que nadie lo note; la política escalonada del bus reintenta y, agotada, el mensaje va a la cola de error
/// y de ahí sale la alerta. Corre en el consumidor, no en el hilo de una petición, así que un fallo no afecta al API.</summary>
public sealed class clsRecalcularScoresConsumer(
    ICommandHandler<clsRecalcularScoresActivosCommand, clsResultadoDeRecalculoDto> recalcular,
    IConfiguration config,
    ILogger<clsRecalcularScoresConsumer> log)
    : IConsumer<clsRecalcularScoresSolicitado>
{
    /// <summary>Tamaño del lote, configurable sin recompilar.</summary>
    public const string ClaveTamanoDeLote = "Score:TamanoDeLoteRecalculo";

    public async Task Consume(ConsumeContext<clsRecalcularScoresSolicitado> contexto)
    {
        var motivo = contexto.Message.Motivo;
        var tamano = clsConfiguracionDeMensajeria.EnteroDeConfig(config, ClaveTamanoDeLote, 200);

        var r = await recalcular.Handle(
            new clsRecalcularScoresActivosCommand(tamano), contexto.CancellationToken);

        // Revisadas y actualizadas por separado: muchas revisadas y ninguna actualizada indica que el intervalo se puede espaciar.
        log.LogInformation(
            "Recálculo de scores ({Motivo}): {Actualizadas} actualizadas de {Revisadas} revisadas en {Lotes} lote(s).",
            motivo, r.Actualizadas, r.Revisadas, r.Lotes);

        // Latido de la bandeja: se manda siempre, aunque no cambie ningún puntaje persistido. La bandeja recalcula al leer y la
        // urgencia depende del tiempo, así que el orden se mueve solo sin ningún otro evento que lo represente.
        await contexto.Publish(
            new clsBandejaCambio(clsMotivoDeCambio.RefrescoPeriodico, DateTimeOffset.UtcNow),
            contexto.CancellationToken);
    }
}
