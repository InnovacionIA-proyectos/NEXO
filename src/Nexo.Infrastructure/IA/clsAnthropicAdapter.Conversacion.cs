using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// Conversación con herramientas y respuesta en tiempo real (<see cref="IConversacionIaPort"/>) sobre el mismo cliente del proveedor.
///
/// El bucle vive aquí y no en Application porque con el razonamiento adaptativo el modelo emite bloques de pensamiento firmados que
/// hay que devolverle intactos junto a sus peticiones de herramienta; son un detalle del proveedor y no salen del adaptador. Cada
/// petición se recibe como flujo, el texto se entrega al instante y los bloques se ensamblan para armar el siguiente turno.
/// </summary>
public sealed partial class clsAnthropicAdapter : IConversacionIaPort
{
    public Task VerificarAsync(clsFuncionIa funcion, Guid? usuarioId, CancellationToken ct = default)
    {
        ExigirDisponible(funcion);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<clsEventoConversacionIa> ConversarAsync(
        clsPeticionConversacionIa peticion,
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutarHerramienta,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var cliente = ExigirDisponible(peticion.Funcion);

        List<ToolUnion> herramientas = [.. peticion.Herramientas.Select(h => (ToolUnion)ComoHerramienta(h))];
        var mensajes = peticion.Mensajes.Select(m => string.Equals(m.Rol, clsMensajeIa.RolAsistente, StringComparison.OrdinalIgnoreCase)
            ? (MessageParam)new AssistantMessageParam { Content = m.Contenido }
            : new UserMessageParam { Content = m.Contenido }).ToList();

        for (var peticiones = 1; ; peticiones++)
        {
            var parametros = ArmarConversacion(peticion, mensajes, herramientas);
            var ensamblador = new clsEnsambladorDeRespuesta();
            var reloj = Stopwatch.StartNew();

            var enumerador = cliente.Messages.CreateStreaming(parametros, ct).GetAsyncEnumerator(ct);
            try
            {
                while (true)
                {
                    RawMessageStreamEvent? evento = null;
                    Exception? fallo = null;
                    try
                    {
                        if (!await enumerador.MoveNextAsync()) break;
                        evento = enumerador.Current;
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        fallo = ex;
                    }

                    if (fallo is not null)
                    {
                        var (codigo, mensaje) = Traducir(fallo);
                        _log.LogWarning("IA {Funcion} (conversación) falló: {Codigo} ({Tipo}) tras {Ms} ms. Proveedor: {Detalle}",
                            peticion.Funcion, codigo, fallo.GetType().Name, reloj.ElapsedMilliseconds, DetalleDelProveedor(fallo));
                        throw new clsServiceUnavailableException(codigo, mensaje);
                    }

                    var delta = ensamblador.Procesar(evento!);
                    if (delta is not null) yield return new clsEventoConversacionIa.Texto(delta);
                }
            }
            finally
            {
                await enumerador.DisposeAsync();
            }

            reloj.Stop();
            var uso = new clsUsoDeTokens(
                ensamblador.Modelo ?? _opciones.ModeloEfectivo, ensamblador.TokensEntrada, ensamblador.TokensSalida,
                ensamblador.TokensCacheLectura, ensamblador.TokensCacheEscritura);
            yield return new clsEventoConversacionIa.Uso(uso);
            _log.LogInformation(
                "IA {Funcion} (conversación) modelo {Modelo}: {Entrada} tokens de entrada, {Salida} de salida, {Ms} ms, {Motivo}",
                peticion.Funcion, uso.Modelo, uso.Entrada, uso.Salida, reloj.ElapsedMilliseconds, ensamblador.Motivo ?? "sin motivo");

            if (ensamblador.Motivo != "tool_use")
            {
                yield return new clsEventoConversacionIa.Fin(ensamblador.Motivo ?? "end_turn");
                yield break;
            }

            // Pidió herramientas por encima del límite de rondas: se corta en lugar de dejarlo consultar sin fin.
            if (peticiones > peticion.MaxIteraciones)
            {
                yield return new clsEventoConversacionIa.Fin("max_iteraciones");
                yield break;
            }

            mensajes.Add(new AssistantMessageParam { Content = ensamblador.Bloques() });

            List<ContentBlockParam> resultados = [];
            foreach (var uso_ in ensamblador.UsosDeHerramienta())
            {
                var llamada = new clsLlamadaHerramientaIa(uso_.Id, uso_.Nombre, uso_.Entrada);
                yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
                var resultado = await ejecutarHerramienta(llamada, ct);
                yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, resultado);
                resultados.Add(new ToolResultBlockParam { ToolUseID = llamada.Id, Content = resultado.Contenido, IsError = resultado.EsError });
            }
            mensajes.Add(new UserMessageParam { Content = resultados });
        }
    }

    private MessageCreateParams ArmarConversacion(clsPeticionConversacionIa p, List<MessageParam> mensajes, List<ToolUnion> herramientas)
    {
        var salida = new OutputConfig { Effort = EsfuerzoDe(_opciones.Effort) };
        var maxTokens = p.MaxTokensSalida ?? _opciones.MaxTokensEfectivo;

        return _opciones.RazonamientoAdaptativo
            ? new MessageCreateParams
            {
                Model = _opciones.ModeloEfectivo, MaxTokens = maxTokens, System = p.Instrucciones, Messages = mensajes,
                Tools = herramientas, OutputConfig = salida, Thinking = new ThinkingConfigAdaptive(), WorkspaceID = _opciones.WorkspaceIdEfectivo,
            }
            : new MessageCreateParams
            {
                Model = _opciones.ModeloEfectivo, MaxTokens = maxTokens, System = p.Instrucciones, Messages = mensajes,
                Tools = herramientas, OutputConfig = salida, WorkspaceID = _opciones.WorkspaceIdEfectivo,
            };
    }

    private static Tool ComoHerramienta(clsHerramientaIa h)
    {
        using var esquema = JsonDocument.Parse(h.EsquemaEntradaJson);
        var raiz = esquema.RootElement;
        var propiedades = raiz.TryGetProperty("properties", out var p)
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : new Dictionary<string, JsonElement>();
        var requeridas = raiz.TryGetProperty("required", out var r) ? r.EnumerateArray().Select(x => x.GetString()!).ToList() : null;

        return new Tool { Name = h.Nombre, Description = h.Descripcion, InputSchema = new() { Properties = propiedades, Required = requeridas } };
    }

    /// <summary>Arma la respuesta del proveedor a partir de los eventos del flujo, en el orden de sus bloques.</summary>
    private sealed class clsEnsambladorDeRespuesta
    {
        private sealed class clsBloque(string tipo)
        {
            public string Tipo { get; } = tipo;
            public StringBuilder Contenido { get; } = new();
            public string? Firma { get; set; }
            public string? Id { get; set; }
            public string? Nombre { get; set; }
            public string? Datos { get; set; }
        }

        private readonly SortedDictionary<long, clsBloque> _bloques = [];

        public string? Modelo { get; private set; }
        public string? Motivo { get; private set; }
        public long TokensEntrada { get; private set; }
        public long TokensSalida { get; private set; }
        public long TokensCacheLectura { get; private set; }
        public long TokensCacheEscritura { get; private set; }

        /// <summary>Procesa un evento; devuelve el trozo de texto de la respuesta que trae, si trae alguno.</summary>
        public string? Procesar(RawMessageStreamEvent evento)
        {
            if (evento.TryPickStart(out var inicio))
            {
                Modelo = inicio.Message.Model.Raw();
                TokensEntrada = inicio.Message.Usage.InputTokens;
                TokensSalida = inicio.Message.Usage.OutputTokens;
                TokensCacheLectura = inicio.Message.Usage.CacheReadInputTokens ?? 0;
                TokensCacheEscritura = inicio.Message.Usage.CacheCreationInputTokens ?? 0;
            }
            else if (evento.TryPickContentBlockStart(out var abre))
            {
                var bloque = Nuevo(abre.ContentBlock);
                _bloques[abre.Index] = bloque;
                return bloque.Tipo == "text" && bloque.Contenido.Length > 0 ? bloque.Contenido.ToString() : null;
            }
            else if (evento.TryPickContentBlockDelta(out var trozo) && _bloques.TryGetValue(trozo.Index, out var actual))
            {
                if (trozo.Delta.TryPickText(out var texto))
                {
                    actual.Contenido.Append(texto.Text);
                    return texto.Text;
                }
                if (trozo.Delta.TryPickThinking(out var pensamiento)) actual.Contenido.Append(pensamiento.Thinking);
                else if (trozo.Delta.TryPickSignature(out var firma)) actual.Firma = (actual.Firma ?? string.Empty) + firma.Signature;
                else if (trozo.Delta.TryPickInputJson(out var json)) actual.Contenido.Append(json.PartialJson);
            }
            else if (evento.TryPickDelta(out var final))
            {
                Motivo = final.Delta.StopReason?.Raw();
                TokensSalida = final.Usage.OutputTokens;
                if (final.Usage.InputTokens is { } entrada) TokensEntrada = entrada;
                if (final.Usage.CacheReadInputTokens is { } lectura) TokensCacheLectura = lectura;
                if (final.Usage.CacheCreationInputTokens is { } escritura) TokensCacheEscritura = escritura;
            }
            return null;
        }

        private static clsBloque Nuevo(RawContentBlockStartEventContentBlock abierto)
        {
            if (abierto.TryPickText(out var texto))
            {
                var b = new clsBloque("text");
                b.Contenido.Append(texto.Text);
                return b;
            }
            if (abierto.TryPickThinking(out var pensamiento))
            {
                var b = new clsBloque("thinking") { Firma = pensamiento.Signature };
                b.Contenido.Append(pensamiento.Thinking);
                return b;
            }
            if (abierto.TryPickRedactedThinking(out var oculto)) return new clsBloque("redacted_thinking") { Datos = oculto.Data };
            if (abierto.TryPickToolUse(out var herramienta)) return new clsBloque("tool_use") { Id = herramienta.ID, Nombre = herramienta.Name };
            return new clsBloque("otro");
        }

        /// <summary>Los bloques de la respuesta, tal cual los emitió el modelo, para repetirlos como su turno anterior.</summary>
        public List<ContentBlockParam> Bloques()
        {
            List<ContentBlockParam> lista = [];
            foreach (var b in _bloques.Values)
            {
                switch (b.Tipo)
                {
                    case "text" when b.Contenido.Length > 0:
                        lista.Add(new TextBlockParam { Text = b.Contenido.ToString() });
                        break;
                    case "thinking":
                        lista.Add(new ThinkingBlockParam { Thinking = b.Contenido.ToString(), Signature = b.Firma ?? string.Empty });
                        break;
                    case "redacted_thinking":
                        lista.Add(new RedactedThinkingBlockParam { Data = b.Datos ?? string.Empty });
                        break;
                    case "tool_use":
                        lista.Add(new ToolUseBlockParam { ID = b.Id!, Name = b.Nombre!, Input = EntradaDe(b) });
                        break;
                }
            }
            return lista;
        }

        public List<(string Id, string Nombre, JsonElement Entrada)> UsosDeHerramienta() =>
            _bloques.Values.Where(b => b.Tipo == "tool_use")
                .Select(b => (b.Id!, b.Nombre!, JsonDocument.Parse(JsonDe(b)).RootElement.Clone()))
                .ToList();

        /// <summary>La entrada de la herramienta como JSON; si llegó vacía o malformada se deja un objeto vacío y la herramienta responde que falta lo que necesita.</summary>
        private static string JsonDe(clsBloque b)
        {
            var texto = b.Contenido.ToString();
            if (texto.Length == 0) return "{}";
            try
            {
                using var _ = JsonDocument.Parse(texto);
                return texto;
            }
            catch (JsonException)
            {
                return "{}";
            }
        }

        private static Dictionary<string, JsonElement> EntradaDe(clsBloque b)
        {
            using var doc = JsonDocument.Parse(JsonDe(b));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone())
                : [];
        }
    }
}
