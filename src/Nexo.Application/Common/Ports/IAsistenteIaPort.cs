namespace Nexo.Application.Common.Ports;

/// <summary>
/// Cada función de NEXO que usa un modelo de lenguaje. Cada una se enciende y se apaga por separado en la configuración
/// (<c>Anthropic:Funciones:&lt;nombre&gt;</c>), así que activar una no activa las demás.
/// </summary>
public enum clsFuncionIa
{
    ImportacionExcel,
    AsistenteCotizacion,
    ApoyoCubicador,
}

/// <summary>Un turno de la conversación. <paramref name="Rol"/> es <c>user</c> o <c>assistant</c>.</summary>
public sealed record clsMensajeIa(string Rol, string Contenido)
{
    public const string RolUsuario = "user";
    public const string RolAsistente = "assistant";
}

/// <summary>
/// Lo que se le pide al modelo. <paramref name="EsquemaSalidaJson"/> es un JSON Schema: cuando viene, la respuesta se obliga a
/// ese formato y el puerto la devuelve ya validada como JSON en <see cref="clsRespuestaIa.JsonEstructurado"/>.
/// </summary>
/// <param name="Instrucciones">Texto fijo que gobierna al modelo (no contiene datos del usuario).</param>
/// <param name="MaxTokensSalida">Tope de tokens de la respuesta; sin valor se usa el de la configuración.</param>
public sealed record clsPeticionIa(
    clsFuncionIa Funcion,
    string Instrucciones,
    IReadOnlyList<clsMensajeIa> Mensajes,
    string? EsquemaSalidaJson = null,
    int? MaxTokensSalida = null);

/// <summary>Respuesta del modelo con lo necesario para medir el uso: modelo, tokens y latencia.</summary>
/// <param name="JsonEstructurado">El mismo texto como JSON válido; solo cuando la petición pidió un esquema de salida.</param>
public sealed record clsRespuestaIa(
    string Texto,
    string? JsonEstructurado,
    string Modelo,
    long TokensEntrada,
    long TokensSalida,
    TimeSpan Latencia);

/// <summary>
/// Salida hacia un modelo de lenguaje.
///
/// La llave del proveedor vive solo en el servidor: ningún cliente (front) llama al proveedor, llaman a un endpoint de NEXO y
/// NEXO usa este puerto. Sin llave, o con la función apagada, la aplicación arranca igual y quien llama decide cómo degradar
/// (<see cref="Habilitada"/>); si llama de todos modos, el puerto responde con una excepción de servicio no disponible y un
/// código estable.
///
/// Códigos de <see cref="Exceptions.clsServiceUnavailableException"/>:
/// <c>IA_NO_CONFIGURADA</c>, <c>IA_FUNCION_DESHABILITADA</c> (nunca se llamó al proveedor);
/// <c>IA_TIEMPO_AGOTADO</c>, <c>IA_LIMITE_PROVEEDOR</c>, <c>IA_NO_DISPONIBLE</c> (pasajeros, reintentar tiene sentido);
/// <c>IA_CREDENCIAL_INVALIDA</c>, <c>IA_PETICION_RECHAZADA</c> (reintentar no lo arregla);
/// <c>IA_RECHAZADA</c> (el modelo se negó), <c>IA_RESPUESTA_TRUNCADA</c> y <c>IA_RESPUESTA_INVALIDA</c> (la respuesta no sirve).
/// Los mensajes nunca incluyen la llave ni el contenido de la petición.
/// </summary>
public interface IAsistenteIaPort
{
    /// <summary>True si hay llave configurada y la función está encendida.</summary>
    bool Habilitada(clsFuncionIa funcion);

    /// <exception cref="Exceptions.clsServiceUnavailableException">Ver los códigos en la descripción del puerto.</exception>
    Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default);
}
