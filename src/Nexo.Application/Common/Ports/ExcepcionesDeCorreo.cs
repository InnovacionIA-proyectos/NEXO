namespace Nexo.Application.Common.Ports;

/// <summary>El correo no se puede enviar y reintentarlo no cambia nada: va derecho a la cola de error en vez de agotar los reintentos.</summary>
public sealed class clsCorreoRechazadoException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <summary>La salida de correo no respondió o falló por algo pasajero; es reintentable.</summary>
public sealed class clsCorreoTransitorioException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);
