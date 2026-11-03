using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes;
using Nexo.Mensajes.Cubicacion;
using MassTransit;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// Implementa <see cref="ICubicacionPort"/> con <see cref="IPublishEndpoint"/> de
/// MassTransit directo — MassTransit real, con el Outbox transaccional de NEXO
/// (decisión del equipo: MassTransit nativo, todo sigue viajando por RabbitMQ).
///
/// NO usa <see cref="IPublicadorDeMensajes"/> (el puerto genérico del resto de
/// NEXO) porque ese puerto no expone forma de fijar el ROUTING KEY en el
/// publish — y sin `cubicacion.solicitada` como routing key, el mensaje llega
/// al exchange `cubicador.eventos` pero no calza con el binding de la cola de
/// Cubicador y se pierde en silencio (topic exchange: sin bind que matchee, no
/// hay error, simplemente no se entrega a nadie - confirmado en runtime: la
/// primera versión de este adaptador publicaba "bien" según las métricas del
/// exchange, `publish_in: 1`, pero cero mensajes llegaban a `cubicador.
/// optimizar`). El exchange/tipo SÍ se configuran en la topología de
/// publicación de clsMensajeriaDependencyInjection.cs
/// (`cfg.Message&lt;clsCubicacionSolicitada&gt;()`/`cfg.Publish&lt;
/// clsCubicacionSolicitada&gt;()`) - solo el routing key necesita fijarse aquí,
/// por mensaje, al momento de publicar.
///
/// La validación de invariantes (que <see cref="IPublicadorDeMensajes"/> da
/// gratis vía `clsPublicadorValidado`) se replica a mano aquí mismo, resolviendo
/// el mismo <see cref="IValidadorDeMensaje{T}"/> que usaría ese decorador -
/// mismo criterio ("todo mensaje que sale al broker debe declarar sus
/// invariantes"), sin poder reutilizar el decorador porque este publish no
/// pasa por <see cref="IPublicadorDeMensajes"/>.
/// </summary>
public sealed class clsCubicacionPublicadorAdapter(
    IPublishEndpoint publicador, IValidadorDeMensaje<clsCubicacionSolicitada> validador)
    : ICubicacionPort
{
    public Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct)
    {
        var planIdTexto = planCargueId.ToString();
        return PublicarAsync(
            "optimizar", planIdTexto,
            new clsSolicitudDeCubicacion(estrategia, algoritmo, semilla, null, respetarBloqueadas), ct);
    }

    public Task PublicarComandoAsync(string tipo, string idCorrelacion, object solicitud, CancellationToken ct) =>
        PublicarAsync(tipo, idCorrelacion, solicitud, ct);

    private Task PublicarAsync(string tipo, string idCorrelacion, object solicitud, CancellationToken ct)
    {
        var mensaje = new clsCubicacionSolicitada(tipo, idCorrelacion, idCorrelacion, solicitud);

        var motivos = validador.Validar(mensaje);
        if (motivos.Count > 0)
            throw new clsValidationFailedException(
                "MENSAJE_INVALIDO",
                $"El mensaje {nameof(clsCubicacionSolicitada)} no cumple sus invariantes y no se publicó.",
                motivos);

        return publicador.Publish(mensaje, contexto => contexto.SetRoutingKey("cubicacion.solicitada"), ct);
    }
}
