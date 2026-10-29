using System.Text.Json;

namespace Nexo.Application.Common.Ports;

/// <summary>Una herramienta que se le ofrece al modelo. <paramref name="EsquemaEntradaJson"/> es el JSON Schema de su entrada.</summary>
public sealed record clsHerramientaIa(string Nombre, string Descripcion, string EsquemaEntradaJson);

/// <summary>Lo que el modelo pidió ejecutar. <paramref name="Id"/> lo asigna el proveedor y enlaza la petición con su resultado.</summary>
public sealed record clsLlamadaHerramientaIa(string Id, string Nombre, JsonElement Entrada);

/// <summary>
/// Lo que se le devuelve al modelo de una herramienta. <paramref name="EsError"/> le dice que la consulta no se pudo hacer para que lo
/// explique; <paramref name="Fuente"/> y <paramref name="Encontrado"/> no viajan al modelo (ya van dentro del contenido), son para quien
/// transmite la conversación.
/// </summary>
public sealed record clsResultadoHerramientaIa(string Contenido, bool EsError, string? Fuente = null, bool? Encontrado = null);

/// <summary>
/// Una conversación con herramientas. <paramref name="Mensajes"/> es el historial de texto (el último es del usuario);
/// <paramref name="MaxIteraciones"/> acota cuántas veces seguidas el modelo puede pedir herramientas antes de responder.
/// </summary>
public sealed record clsPeticionConversacionIa(
    clsFuncionIa Funcion,
    string Instrucciones,
    IReadOnlyList<clsMensajeIa> Mensajes,
    IReadOnlyList<clsHerramientaIa> Herramientas,
    int MaxIteraciones,
    int? MaxTokensSalida = null,
    Guid? UsuarioId = null);

/// <summary>Lo que ocurre durante una conversación, en el orden en que ocurre.</summary>
public abstract record clsEventoConversacionIa
{
    /// <summary>Un trozo del texto de la respuesta, tal como lo va generando el modelo.</summary>
    public sealed record Texto(string Delta) : clsEventoConversacionIa;

    /// <summary>El modelo pidió una herramienta y se va a ejecutar.</summary>
    public sealed record HerramientaSolicitada(clsLlamadaHerramientaIa Llamada) : clsEventoConversacionIa;

    /// <summary>La herramienta terminó (bien o con error) y su resultado vuelve al modelo.</summary>
    public sealed record HerramientaTerminada(clsLlamadaHerramientaIa Llamada, clsResultadoHerramientaIa Resultado) : clsEventoConversacionIa;

    /// <summary>Lo que gastó una llamada al proveedor ya terminada; cada petición del bucle de herramientas emite una.</summary>
    public sealed record Uso(clsUsoDeTokens Tokens) : clsEventoConversacionIa;

    /// <summary>La respuesta terminó. <paramref name="Motivo"/>: <c>end_turn</c>, <c>max_tokens</c>, <c>refusal</c> o <c>max_iteraciones</c>.</summary>
    public sealed record Fin(string Motivo) : clsEventoConversacionIa;
}

/// <summary>
/// Conversación con el modelo con respuesta en tiempo real y herramientas. El puerto corre el bucle completo (pedir, ejecutar las
/// herramientas que el modelo solicite con <c>ejecutarHerramienta</c>, devolverle los resultados y seguir) y lo cuenta como eventos;
/// quien lo usa nunca toca los bloques propios del proveedor.
///
/// Los errores del proveedor salen como <see cref="Exceptions.clsServiceUnavailableException"/> con los mismos códigos de
/// <see cref="IAsistenteIaPort"/>; pueden ocurrir al empezar o a la mitad (con parte del texto ya entregado).
/// </summary>
public interface IConversacionIaPort
{
    /// <summary>
    /// Falla antes de hacer nada si la función está apagada o sin configurar (<c>IA_APAGADA</c>, <c>IA_NO_CONFIGURADA</c>,
    /// <c>IA_FUNCION_DESHABILITADA</c>) o si la persona ya consumió su tope diario (<c>429 IA_TOPE_DIARIO</c>). Se llama antes de abrir
    /// la respuesta para que esos casos sean un error HTTP normal y no un corte a mitad de la transmisión.
    /// </summary>
    Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default);

    IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion,
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
        CancellationToken ct = default);
}
