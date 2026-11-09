namespace Nexo.Application.Common.Ports;

/// <summary>Cada función de NEXO que usa un modelo de lenguaje; se enciende y apaga por separado en <c>Anthropic:Funciones:&lt;nombre&gt;</c>.</summary>
public enum clsFuncionIa
{
    ImportacionExcel,
    AsistenteCotizacion,
    ApoyoCubicador,
}

/// <summary>Motivos por los que una función con IA responde sin ella; el resto son los códigos <c>IA_*</c> del proveedor.</summary>
public static class clsMotivosSinIa
{
    /// <summary>Sin llave, IA apagada o función apagada en este ambiente: no hubo llamada al proveedor.</summary>
    public const string Desactivada = "IA_DESACTIVADA";

    /// <summary>El modelo llegó al límite de tokens de salida antes de terminar.</summary>
    public const string RespuestaTruncada = "IA_RESPUESTA_TRUNCADA";
}

/// <summary>Un turno de la conversación. <paramref name="Rol"/> es <c>user</c> o <c>assistant</c>.</summary>
public sealed record clsMensajeIa(string Rol, string Contenido)
{
    public const string RolUsuario = "user";
    public const string RolAsistente = "assistant";
}

/// <summary>
/// Con <paramref name="EsquemaSalidaJson"/> (JSON Schema) la respuesta se obliga a ese formato y vuelve validada en <see cref="clsRespuestaIa.JsonEstructurado"/>.
/// </summary>
/// <param name="MaxTokensSalida">Sin valor se usa el de la configuración.</param>
/// <param name="UsuarioId">Con valor, la llamada cuenta para su tope diario; sin valor (proceso interno) no hay tope por persona.</param>
public sealed record clsPeticionIa(
    clsFuncionIa Funcion,
    string Instrucciones,
    IReadOnlyList<clsMensajeIa> Mensajes,
    string? EsquemaSalidaJson = null,
    int? MaxTokensSalida = null,
    Guid? UsuarioId = null);

/// <summary>Respuesta del modelo con lo necesario para medir el uso: modelo, tokens y latencia.</summary>
/// <param name="JsonEstructurado">El mismo texto como JSON válido; solo cuando la petición pidió un esquema de salida.</param>
public sealed record clsRespuestaIa(
    string Texto,
    string? JsonEstructurado,
    string Modelo,
    long TokensEntrada,
    long TokensSalida,
    TimeSpan Latencia,
    long TokensCacheLectura = 0,
    long TokensCacheEscritura = 0);

/// <summary>Lo que gastó una llamada, para poder registrarlo aunque su respuesta no sirviera.</summary>
public sealed record clsUsoDeTokens(string Modelo, long Entrada, long Salida, long CacheLectura, long CacheEscritura);

/// <summary>El proveedor respondió (y cobró) pero la respuesta no sirve (rechazo, truncada o formato inválido); lleva lo gastado para el registro de uso.</summary>
public sealed class clsIaRespuestaNoUtilException(string code, string message, clsUsoDeTokens uso)
    : Exceptions.clsServiceUnavailableException(code, message)
{
    public clsUsoDeTokens Uso { get; } = uso;
}

/// <summary>
/// Salida hacia un modelo de lenguaje; la llave vive solo en el servidor. Sin llave o con la función apagada la aplicación arranca igual
/// y quien llama decide cómo degradar (<see cref="Habilitada"/>); si llama, lanza <see cref="Exceptions.clsServiceUnavailableException"/> con códigos IA_*
/// (sin la llave ni el contenido de la petición). El registro de uso responde 429 IA_TOPE_DIARIO al superar el tope diario.
/// </summary>
public interface IAsistenteIaPort
{
    /// <summary>True si hay llave configurada y la función está encendida.</summary>
    bool Habilitada(clsFuncionIa funcion);

    /// <exception cref="Exceptions.clsServiceUnavailableException">Ver los códigos en la descripción del puerto.</exception>
    Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default);
}
