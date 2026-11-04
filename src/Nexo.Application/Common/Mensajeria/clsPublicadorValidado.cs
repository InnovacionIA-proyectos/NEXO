using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes;

namespace Nexo.Application.Common.Mensajeria;

/// <summary>
/// Decorador que valida el mensaje antes de delegar. Falla cerrado: un tipo de mensaje sin validador declarado NO se publica.
/// </summary>
public sealed class clsPublicadorValidado(IPublicadorDeMensajes interno, IServiceProvider servicios)
    : IPublicadorDeMensajes
{
    public Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        // Resolución en runtime: el método es genérico y no se puede inyectar IValidadorDeMensaje<T> en un tipo no genérico.
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
