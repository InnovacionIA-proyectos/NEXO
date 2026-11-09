using System.Text.Json;

namespace Nexo.Application.Common.Ports;

/// <summary>Una herramienta que se le ofrece al modelo. <paramref name="EsquemaEntradaJson"/> es el JSON Schema de su entrada.</summary>
public sealed record clsHerramientaIa(string Nombre, string Descripcion, string EsquemaEntradaJson);

/// <summary>Lo que el modelo pidió ejecutar. <paramref name="Id"/> lo asigna el proveedor y enlaza la petición con su resultado.</summary>
public sealed record clsLlamadaHerramientaIa(string Id, string Nombre, JsonElement Entrada);

/// <summary>Resultado de una herramienta para el modelo; <paramref name="Fuente"/>, <paramref name="Encontrado"/> y <paramref name="Resumen"/> no viajan al modelo, son para quien transmite la conversación.</summary>
public sealed record clsResultadoHerramientaIa(string Contenido, bool EsError, string? Fuente = null, bool? Encontrado = null, string? Resumen = null);

/// <summary><paramref name="MaxIteraciones"/> acota cuántas veces seguidas el modelo puede pedir herramientas antes de responder.</summary>
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
/// Conversación con respuesta en tiempo real y herramientas: el puerto corre el bucle completo y lo cuenta como eventos.
/// Los errores salen como <see cref="Exceptions.clsServiceUnavailableException"/> (códigos de <see cref="IAsistenteIaPort"/>), también a mitad de la respuesta.
/// </summary>
public interface IConversacionIaPort
{
    /// <summary>
    /// Falla antes de abrir la respuesta si la función está apagada o sin configurar, o si la persona superó su tope diario (429 IA_TOPE_DIARIO).
    /// </summary>
    Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default);

    IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion,
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
        CancellationToken ct = default);
}
