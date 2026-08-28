namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// El correo no se puede enviar y reintentarlo no va a cambiar nada.
///
/// PARA QUÉ SIRVE DISTINGUIRLO. Un adjunto que pesa más de lo permitido, una dirección
/// mal formada o un cuerpo que el relay rechaza van a fallar igual las cuatro veces. Con
/// una sola clase de excepción, esos casos consumen la política de reintentos completa
/// —hasta un minuto y medio con los intervalos actuales— antes de que alguien se entere.
///
/// La política del bus está configurada para NO reintentar esta: va derecho a la cola de
/// error, y la alerta llega al primer intento en vez de al cuarto.
/// </summary>
public sealed class CorreoRechazadoException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <summary>
/// La salida de correo no respondió, o falló por algo pasajero.
///
/// El relay caído, la red, un 500. Reintentar tiene todo el sentido: es exactamente el
/// caso para el que existe la política escalonada. Si se agotan los intentos, el mensaje
/// queda en la cola de error y de ahí sale la alerta.
/// </summary>
public sealed class CorreoTransitorioException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);
