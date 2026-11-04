namespace Nexo.Application.Common.Ports;

public sealed record clsAdjuntoDeCorreo(string Nombre, byte[] Contenido);

/// <param name="Metadatos">
/// Pares clave/valor que la salida guarda junto al correo. Van como diccionario porque el relay exige JSON válido
/// y rechaza una cadena suelta con 422; el adaptador se encarga del formato.
/// </param>
public sealed record clsCorreoParaEnviar(
    IReadOnlyList<string> Destinatarios,
    string Asunto,
    string CuerpoHtml,
    IReadOnlyList<clsAdjuntoDeCorreo> Adjuntos,
    IReadOnlyDictionary<string, string>? Metadatos = null);

/// <summary>Salida de correo corporativo; devuelve el id del relay para consultar su estado, o null si el adaptador no lo provee.</summary>
public interface ICorreoPort
{
    /// <summary>Si no está configurado, quien llama decide cómo degradar.</summary>
    bool Configurado { get; }

    /// <returns>Identificador del correo en el relay, o null si no lo provee.</returns>
    /// <exception cref="clsCorreoRechazadoException">No se puede enviar y reintentar no cambia nada.</exception>
    /// <exception cref="clsCorreoTransitorioException">Falla pasajera; reintentar tiene sentido.</exception>
    Task<string?> EnviarAsync(clsCorreoParaEnviar correo, CancellationToken ct = default);
}
