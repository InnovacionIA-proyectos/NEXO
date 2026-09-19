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

/// <summary>
/// Hace el recálculo que la saga pidió.
///
/// LA EXCEPCIÓN SUBE A PROPÓSITO. Es lo contrario del consumidor de avisos de la #17: allá
/// un fallo se registra y se sigue, porque un navegador desconectado no es una avería.
/// Aquí sí lo es — si el score persistido deja de refrescarse, los reportes empiezan a
/// mentir sin que nadie lo note. Dejando subir la excepción, la política escalonada del
/// bus reintenta y, si los agota, el mensaje va a la cola de error, que es de donde la
/// #13 saca la alerta al equipo.
///
/// Y ESE FALLO NO AFECTA AL API, que es lo que pide el criterio de cierre: esto corre en
/// el consumidor, no en el hilo de una petición. Antes vivía en un BackgroundService del
/// mismo proceso, donde un fallo solo dejaba una línea en el log y se esperaba quince
/// minutos a la siguiente vuelta.
/// </summary>
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

        // Revisadas y actualizadas por separado: si se revisan cientos y no se actualiza
        // ninguna, el intervalo está corriendo de más y se puede espaciar. Con un solo
        // número eso no se ve en el log.
        log.LogInformation(
            "Recálculo de scores ({Motivo}): {Actualizadas} actualizadas de {Revisadas} revisadas en {Lotes} lote(s).",
            motivo, r.Actualizadas, r.Revisadas, r.Lotes);

        // EL LATIDO DE LA BANDEJA, y conviene entender por qué se manda siempre y no solo
        // cuando cambió algún puntaje persistido.
        //
        // Refrescar el puntaje persistido NO cambia lo que la bandeja muestra: la bandeja
        // lo recalcula al leer. Lo que sí cambió desde el aviso anterior es EL TIEMPO, y la
        // urgencia depende del ahora — el orden se mueve solo, de forma continua, sin que
        // nadie toque nada.
        //
        // Ese movimiento no tiene ningún evento que lo represente. Este es el único aviso
        // que lo cubre, y por eso sale aunque se hayan actualizado cero filas.
        await contexto.Publish(
            new clsBandejaCambio(clsMotivoDeCambio.RefrescoPeriodico, DateTimeOffset.UtcNow),
            contexto.CancellationToken);
    }
}
