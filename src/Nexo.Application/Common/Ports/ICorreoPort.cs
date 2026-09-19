namespace Nexo.Application.Common.Ports;

/// <summary>Un archivo que va adjunto al correo.</summary>
public sealed record AdjuntoDeCorreo(string Nombre, byte[] Contenido);

/// <summary>
/// Un correo listo para salir.
///
/// Los destinatarios van en lista y no en un solo campo porque el relay corporativo
/// admite varios, y porque la #22 va a derivar el destinatario de la sesión — mejor que
/// la forma del puerto no tenga que cambiar otra vez para eso.
/// </summary>
/// <param name="Metadatos">
/// Pares clave/valor que la salida guarda junto al correo. Sirven para responder "¿se
/// envió la notificación de esta cotización?" sin cruzar logs a mano.
///
/// VAN COMO DICCIONARIO Y NO COMO TEXTO a propósito. El relay corporativo exige que los
/// metadatos sean un documento JSON válido, y mandarle una cadena suelta se rechaza con
/// 422. Con un diccionario, quien llama expresa lo que quiere guardar y el adaptador se
/// encarga del formato — que es asunto del transporte, no del negocio.
///
/// Se descubrió mandando un correo de verdad: las pruebas contra un servidor simulado no
/// podían verlo porque el simulado acepta cualquier cosa.
/// </param>
public sealed record CorreoParaEnviar(
    IReadOnlyList<string> Destinatarios,
    string Asunto,
    string CuerpoHtml,
    IReadOnlyList<AdjuntoDeCorreo> Adjuntos,
    IReadOnlyDictionary<string, string>? Metadatos = null);

/// <summary>
/// Salida de correo corporativo.
///
/// Vive en Common/Ports y no dentro de un caso de uso porque mandar un correo no es
/// asunto de Cotizaciones: hoy lo usa la notificación de entrega, mañana cualquier otra
/// cosa.
///
/// DEVUELVE UN IDENTIFICADOR cuando quien está detrás lo da. El relay corporativo acepta
/// el correo y lo encola: responde con un id con el que se puede consultar su estado y
/// sus intentos. Guardarlo en el log es lo que permite responder "¿salió o no?" sin
/// entrar al servidor de correo. Un adaptador que no tenga id devuelve null.
/// </summary>
public interface ICorreoPort
{
    /// <summary>Si no está configurado, quien llama decide cómo degradar.</summary>
    bool Configurado { get; }

    /// <summary>
    /// Entrega el correo a la salida corporativa.
    /// </summary>
    /// <returns>Identificador del correo en el relay, o null si no lo provee.</returns>
    /// <exception cref="CorreoRechazadoException">
    /// El correo no se puede enviar y reintentarlo no va a cambiar nada.
    /// </exception>
    /// <exception cref="CorreoTransitorioException">
    /// La salida no respondió o falló por algo pasajero. Reintentar tiene sentido.
    /// </exception>
    Task<string?> EnviarAsync(CorreoParaEnviar correo, CancellationToken ct = default);
}
