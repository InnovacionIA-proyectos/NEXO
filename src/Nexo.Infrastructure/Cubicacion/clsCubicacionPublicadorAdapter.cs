using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes;
using Nexo.Mensajes.Cubicacion;
using MassTransit;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>Implementa ICubicacionPort con el IPublishEndpoint de MassTransit (Outbox transaccional). No usa IPublicadorDeMensajes
/// porque no permite fijar el ROUTING KEY: sin `cubicacion.solicitada` el mensaje llega al exchange `cubicador.eventos` pero no calza
/// con el binding de la cola y se pierde en silencio. La validación de invariantes se replica aquí con el mismo
/// IValidadorDeMensaje{T} que usaría el decorador.</summary>
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
