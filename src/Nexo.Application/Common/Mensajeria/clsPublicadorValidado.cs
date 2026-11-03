using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes;

namespace Nexo.Application.Common.Mensajeria;

/// <summary>
/// Decorador del puerto de publicación: valida el mensaje y delega. Nada llega al
/// broker sin pasar por aquí.
///
/// Por qué un decorador y no validar dentro del adaptador de MassTransit: la política
/// de validación no tiene nada que ver con el transporte. Así el adaptador sigue
/// siendo un envoltorio de tres líneas, los handlers no cambian, y agregar o quitar
/// la validación es un cambio de registro en el contenedor — abierto a extensión,
/// cerrado a modificación.
///
/// Y por qué vive en Application y no en Infrastructure: solo depende del puerto y de
/// los validadores. No conoce MassTransit, ni colas, ni nada de infraestructura. Si
/// mañana el transporte cambia, este archivo no se entera.
///
/// FALLA CERRADO. Un tipo de mensaje sin validador declarado NO se publica. Sin esa
/// regla, la garantía sería una intención: bastaría con que alguien agregue un mensaje
/// nuevo y olvide su validador para que empiece a viajar sin revisar, en silencio.
/// Preferimos que se rompa en la primera prueba a que se rompa en producción dentro
/// de un consumidor, minutos después y lejos de la causa.
/// </summary>
public sealed class clsPublicadorValidado(IPublicadorDeMensajes interno, IServiceProvider servicios)
    : IPublicadorDeMensajes
{
    public Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        // Resolución en tiempo de ejecución porque el método es genérico: no se puede
        // inyectar IValidadorDeMensaje<T> en un tipo no genérico. Queda contenido en
        // esta única línea, que es el precio de tener un solo puerto para todos los
        // mensajes en vez de uno por tipo.
        if (servicios.GetService(typeof(IValidadorDeMensaje<T>)) is not IValidadorDeMensaje<T> validador)
            throw new clsValidationFailedException(
                "MENSAJE_SIN_VALIDADOR",
                $"El mensaje {typeof(T).Name} no declara validador y no puede publicarse. " +
                "Todo mensaje que sale al broker debe declarar sus invariantes.");

        var motivos = validador.Validar(mensaje);
        if (motivos.Count > 0)
            throw new clsValidationFailedException(
                "MENSAJE_INVALIDO",
                $"El mensaje {typeof(T).Name} no cumple sus invariantes y no se publicó.",
                motivos);

        return interno.PublicarAsync(mensaje, ct);
    }
}
