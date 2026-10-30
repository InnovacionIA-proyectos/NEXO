using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.IA;

namespace Nexo.Api.Tests.IA;

/// <summary>
/// La conversación con herramientas del adaptador de Claude contra un proveedor SIMULADO que responde en el formato de eventos del
/// proveedor: ninguna prueba sale a la red ni usa una llave real. Se verifica lo que el adaptador ENVÍA (herramientas, flujo, esfuerzo,
/// ausencia de temperature/top_p/top_k, y que el segundo turno devuelva los bloques de razonamiento y los resultados de herramienta) y
/// cómo ensambla lo que recibe.
/// </summary>
public class clsAnthropicConversacionTests
{
    private const string Llave = "sk-ant-LLAVE-FALSA-DE-PRUEBA";

    private sealed class clsProveedorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private int _llamadas;
        public List<string> Cuerpos { get; } = [];
        public int Llamadas => _llamadas;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            return responder(Interlocked.Increment(ref _llamadas));
        }
    }

    // ------------------------------------------------------------ el flujo de eventos del proveedor

    private static string Evento(string tipo, object datos) => $"event: {tipo}\ndata: {JsonSerializer.Serialize(datos)}\n\n";

    private static string Inicio(int entrada = 20, int cacheLectura = 0) => Evento("message_start", new
    {
        type = "message_start",
        message = new
        {
            id = "msg_1", type = "message", role = "assistant", model = "claude-sonnet-5-5", content = Array.Empty<object>(),
            stop_reason = (string?)null, stop_sequence = (string?)null,
            usage = new { input_tokens = entrada, output_tokens = 1, cache_read_input_tokens = cacheLectura, cache_creation_input_tokens = 0 },
        },
    });

    private static string Abre(int indice, object bloque) => Evento("content_block_start", new { type = "content_block_start", index = indice, content_block = bloque });

    private static string Delta(int indice, object delta) => Evento("content_block_delta", new { type = "content_block_delta", index = indice, delta });

    private static string Cierra(int indice) => Evento("content_block_stop", new { type = "content_block_stop", index = indice });

    private static string Final(string motivo, int salida = 12) => Evento("message_delta", new
    {
        type = "message_delta", delta = new { stop_reason = motivo, stop_sequence = (string?)null }, usage = new { output_tokens = salida },
    }) + Evento("message_stop", new { type = "message_stop" });

    private static HttpResponseMessage Flujo(params string[] eventos) =>
        new(HttpStatusCode.OK) { Content = new StringContent(string.Concat(eventos), Encoding.UTF8, "text/event-stream") };

    private static HttpResponseMessage Texto(string[] trozos, string motivo = "end_turn", int entrada = 20, int salida = 12) =>
        Flujo([
            Inicio(entrada), Abre(0, new { type = "text", text = "" }),
            .. trozos.Select(t => Delta(0, new { type = "text_delta", text = t })),
            Cierra(0), Final(motivo, salida),
        ]);

    /// <summary>Razonamiento firmado, un texto y una petición de herramienta cuya entrada llega partida en dos trozos de JSON.</summary>
    private static HttpResponseMessage PideHerramienta(string nombre = "consultar_piso_sicetac", string[]? json = null) =>
        Flujo([
            Inicio(30),
            Abre(0, new { type = "thinking", thinking = "", signature = "" }),
            Delta(0, new { type = "thinking_delta", thinking = "Necesito el piso." }),
            Delta(0, new { type = "signature_delta", signature = "FIRMA-DEL-PROVEEDOR" }),
            Cierra(0),
            Abre(1, new { type = "text", text = "" }),
            Delta(1, new { type = "text_delta", text = "Consulto el piso. " }),
            Cierra(1),
            Abre(2, new { type = "tool_use", id = "toolu_01", name = nombre, input = new { } }),
            .. (json ?? ["{\"origen\":\"Mede", "llín\",\"destino\":\"Bogotá\"}"]).Select(j => Delta(2, new { type = "input_json_delta", partial_json = j })),
            Cierra(2),
            Final("tool_use", 40)]);

    private static HttpResponseMessage Error(HttpStatusCode estado, string tipo) =>
        new(estado) { Content = new StringContent(JsonSerializer.Serialize(new { type = "error", error = new { type = tipo, message = "detalle del proveedor" } }), Encoding.UTF8, "application/json") };

    // ------------------------------------------------------------ armado

    private static clsAnthropicAdapter Adaptador(clsProveedorFalso proveedor, Action<clsOpcionesAnthropic>? ajustar = null, bool conLlave = true)
    {
        var opciones = new clsOpcionesAnthropic
        {
            ApiKey = conLlave ? Llave : null,
            MaxReintentos = 0,
            Funciones = new(StringComparer.OrdinalIgnoreCase) { ["AsistenteCotizacion"] = true },
        };
        ajustar?.Invoke(opciones);
        return new clsAnthropicAdapter(
            Options.Create(opciones), NullLogger<clsAnthropicAdapter>.Instance, new HttpClient(proveedor), "http://proveedor.prueba");
    }

    private static readonly clsHerramientaIa Piso = new(
        "consultar_piso_sicetac", "Piso SICE-TAC de una ruta.",
        """{"type":"object","additionalProperties":false,"required":["origen","destino"],"properties":{"origen":{"type":"string"},"destino":{"type":"string"}}}""");

    private static clsPeticionConversacionIa Peticion(int maxIteraciones = 4) => new(
        clsFuncionIa.AsistenteCotizacion, "Eres el asistente.",
        [new clsMensajeIa(clsMensajeIa.RolUsuario, "Hola"), new clsMensajeIa(clsMensajeIa.RolAsistente, "Dime."), new clsMensajeIa(clsMensajeIa.RolUsuario, "¿Piso Medellín-Bogotá?")],
        [Piso], maxIteraciones, 1024, Guid.NewGuid());

    private static Task<clsResultadoHerramientaIa> SinHerramienta(clsLlamadaHerramientaIa _, CancellationToken __) =>
        throw new InvalidOperationException("No debía ejecutarse ninguna herramienta.");

    private static async Task<List<clsEventoConversacionIa>> Correr(
        clsAnthropicAdapter a, clsPeticionConversacionIa p, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>>? herramienta = null)
    {
        List<clsEventoConversacionIa> lista = [];
        await foreach (var e in a.ConversarAsync(p, herramienta ?? SinHerramienta)) lista.Add(e);
        return lista;
    }

    // ------------------------------------------------------------ lo que se envía

    [Fact]
    public async Task Envia_el_flujo_las_herramientas_el_esfuerzo_y_el_historial_sin_temperature_top_p_ni_top_k()
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["Hola"]));

        await Correr(Adaptador(proveedor), Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        var raiz = cuerpo.RootElement;
        Assert.True(raiz.GetProperty("stream").GetBoolean());
        Assert.Equal("claude-sonnet-5-5", raiz.GetProperty("model").GetString());
        // `medium` desde #363: lo recomienda la evaluacion del mapeo de Excel. Ver clsOpcionesAnthropic.
        Assert.Equal("medium", raiz.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("adaptive", raiz.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal(1024, raiz.GetProperty("max_tokens").GetInt32());
        Assert.Contains("Eres el asistente.", raiz.GetProperty("system").ToString());
        Assert.Equal(["user", "assistant", "user"], raiz.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()));
        foreach (var prohibido in new[] { "temperature", "top_p", "top_k" }) Assert.False(raiz.TryGetProperty(prohibido, out _), prohibido);

        var herramienta = Assert.Single(raiz.GetProperty("tools").EnumerateArray());
        Assert.Equal("consultar_piso_sicetac", herramienta.GetProperty("name").GetString());
        Assert.Equal("Piso SICE-TAC de una ruta.", herramienta.GetProperty("description").GetString());
        var esquema = herramienta.GetProperty("input_schema");
        Assert.Equal("object", esquema.GetProperty("type").GetString());
        Assert.Equal("string", esquema.GetProperty("properties").GetProperty("origen").GetProperty("type").GetString());
        Assert.Equal(["origen", "destino"], esquema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task Sin_razonamiento_adaptativo_no_envia_el_bloque_thinking()
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["Hola"]));

        await Correr(Adaptador(proveedor, o => o.RazonamientoAdaptativo = false), Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        Assert.False(cuerpo.RootElement.TryGetProperty("thinking", out _));
    }

    // ------------------------------------------------------------ respuesta en tiempo real

    [Fact]
    public async Task El_texto_se_entrega_trozo_a_trozo_en_orden_y_se_informan_tokens_y_final()
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["El piso ", "es ", "alto."], entrada: 25, salida: 9));

        var eventos = await Correr(Adaptador(proveedor), Peticion());

        Assert.Equal(["El piso ", "es ", "alto."], eventos.OfType<clsEventoConversacionIa.Texto>().Select(t => t.Delta));
        var uso = Assert.Single(eventos.OfType<clsEventoConversacionIa.Uso>()).Tokens;
        Assert.Equal(25, uso.Entrada);
        Assert.Equal(9, uso.Salida);
        Assert.Equal("claude-sonnet-5-5", uso.Modelo);
        Assert.Equal("end_turn", Assert.IsType<clsEventoConversacionIa.Fin>(eventos[^1]).Motivo);
        Assert.IsType<clsEventoConversacionIa.Uso>(eventos[^2]);
    }

    [Theory]
    [InlineData("max_tokens")]
    [InlineData("refusal")]
    public async Task Un_final_no_normal_se_informa_como_motivo_del_fin(string motivo)
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["Parte."], motivo));

        var eventos = await Correr(Adaptador(proveedor), Peticion());

        Assert.Equal(motivo, Assert.IsType<clsEventoConversacionIa.Fin>(eventos[^1]).Motivo);
    }

    // ------------------------------------------------------------ el bucle de herramientas

    [Fact]
    public async Task Ejecuta_la_herramienta_pedida_con_su_entrada_completa_y_devuelve_al_modelo_el_razonamiento_y_el_resultado()
    {
        var proveedor = new clsProveedorFalso(n => n == 1 ? PideHerramienta() : Texto(["El piso es $2.900.000."]));
        clsLlamadaHerramientaIa? recibida = null;

        var eventos = await Correr(Adaptador(proveedor), Peticion(), (llamada, _) =>
        {
            recibida = llamada;
            return Task.FromResult(new clsResultadoHerramientaIa("""{"encontrado":true,"valor":{"valorPiso":2900000}}""", EsError: false, "SICE-TAC", true));
        });

        // la herramienta se ejecutó con lo que el modelo armó trozo a trozo
        Assert.Equal("toolu_01", recibida!.Id);
        Assert.Equal("consultar_piso_sicetac", recibida.Nombre);
        Assert.Equal("Medellín", recibida.Entrada.GetProperty("origen").GetString());
        Assert.Equal("Bogotá", recibida.Entrada.GetProperty("destino").GetString());

        // los eventos salen en orden: texto previo, herramienta pedida y terminada, tokens de cada petición, texto final, fin
        Assert.Equal(
            ["Texto", "Uso", "HerramientaSolicitada", "HerramientaTerminada", "Texto", "Uso", "Fin"],
            eventos.Select(e => e.GetType().Name));
        Assert.Equal(2, proveedor.Llamadas);

        // el segundo turno devuelve el razonamiento firmado, la petición y su resultado, en el orden que exige el proveedor
        using var segundo = JsonDocument.Parse(proveedor.Cuerpos[1]);
        var mensajes = segundo.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["user", "assistant", "user", "assistant", "user"], mensajes.Select(m => m.GetProperty("role").GetString()));
        var turnoDelModelo = mensajes[3].GetProperty("content").EnumerateArray().ToList();
        Assert.Equal(["thinking", "text", "tool_use"], turnoDelModelo.Select(b => b.GetProperty("type").GetString()));
        Assert.Equal("Necesito el piso.", turnoDelModelo[0].GetProperty("thinking").GetString());
        Assert.Equal("FIRMA-DEL-PROVEEDOR", turnoDelModelo[0].GetProperty("signature").GetString());
        Assert.Equal("toolu_01", turnoDelModelo[2].GetProperty("id").GetString());
        Assert.Equal("Medellín", turnoDelModelo[2].GetProperty("input").GetProperty("origen").GetString());
        var resultado = Assert.Single(mensajes[4].GetProperty("content").EnumerateArray());
        Assert.Equal("tool_result", resultado.GetProperty("type").GetString());
        Assert.Equal("toolu_01", resultado.GetProperty("tool_use_id").GetString());
        Assert.Contains("2900000", resultado.GetProperty("content").ToString());
    }

    [Fact]
    public async Task Un_error_de_la_herramienta_vuelve_al_modelo_marcado_como_error()
    {
        var proveedor = new clsProveedorFalso(n => n == 1 ? PideHerramienta() : Texto(["No tengo permiso."]));

        await Correr(Adaptador(proveedor), Peticion(), (_, _) =>
            Task.FromResult(new clsResultadoHerramientaIa("""{"error":"HERRAMIENTA_NO_PERMITIDA"}""", EsError: true)));

        using var segundo = JsonDocument.Parse(proveedor.Cuerpos[1]);
        var resultado = segundo.RootElement.GetProperty("messages").EnumerateArray().Last().GetProperty("content").EnumerateArray().Single();
        Assert.True(resultado.GetProperty("is_error").GetBoolean());
    }

    [Fact]
    public async Task Una_entrada_de_herramienta_malformada_llega_como_objeto_vacio_para_que_la_herramienta_diga_que_falta()
    {
        var proveedor = new clsProveedorFalso(n => n == 1 ? PideHerramienta(json: ["{\"origen\":"]) : Texto(["Falta el origen."]));
        JsonElement? entrada = null;

        await Correr(Adaptador(proveedor), Peticion(), (llamada, _) =>
        {
            entrada = llamada.Entrada;
            return Task.FromResult(new clsResultadoHerramientaIa("{}", EsError: true));
        });

        Assert.Equal(JsonValueKind.Object, entrada!.Value.ValueKind);
        Assert.Empty(entrada.Value.EnumerateObject());
    }

    [Fact]
    public async Task Si_el_modelo_sigue_pidiendo_herramientas_pasado_el_limite_se_corta_con_max_iteraciones()
    {
        var proveedor = new clsProveedorFalso(_ => PideHerramienta());
        var ejecuciones = 0;

        var eventos = await Correr(Adaptador(proveedor), Peticion(maxIteraciones: 2), (_, _) =>
        {
            ejecuciones++;
            return Task.FromResult(new clsResultadoHerramientaIa("{}", EsError: false));
        });

        // Dos rondas de herramientas y una petición más, que ya no se atiende.
        Assert.Equal(3, proveedor.Llamadas);
        Assert.Equal(2, ejecuciones);
        Assert.Equal("max_iteraciones", Assert.IsType<clsEventoConversacionIa.Fin>(eventos[^1]).Motivo);
    }

    // ------------------------------------------------------------ errores y apagado

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", "IA_LIMITE_PROVEEDOR")]
    [InlineData(HttpStatusCode.InternalServerError, "api_error", "IA_NO_DISPONIBLE")]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error", "IA_CREDENCIAL_INVALIDA")]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", "IA_PETICION_RECHAZADA")]
    public async Task Los_errores_del_proveedor_salen_como_503_con_codigo_estable_sin_filtrar_la_llave(HttpStatusCode estado, string tipo, string codigo)
    {
        var proveedor = new clsProveedorFalso(_ => Error(estado, tipo));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Correr(Adaptador(proveedor), Peticion()));

        Assert.Equal(codigo, ex.Code);
        Assert.DoesNotContain(Llave, ex.Message);
    }

    [Fact]
    public async Task Verificar_responde_el_codigo_que_corresponde_sin_llamar_al_proveedor()
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["no debía llamarse"]));

        var sinLlave = await Assert.ThrowsAsync<clsServiceUnavailableException>(() =>
            Adaptador(proveedor, conLlave: false).VerificarAsync(clsFuncionIa.AsistenteCotizacion, null));
        var apagada = await Assert.ThrowsAsync<clsServiceUnavailableException>(() =>
            Adaptador(proveedor, o => o.Habilitada = false).VerificarAsync(clsFuncionIa.AsistenteCotizacion, null));
        var funcionApagada = await Assert.ThrowsAsync<clsServiceUnavailableException>(() =>
            Adaptador(proveedor, o => o.Funciones["AsistenteCotizacion"] = false).VerificarAsync(clsFuncionIa.AsistenteCotizacion, null));
        await Adaptador(proveedor).VerificarAsync(clsFuncionIa.AsistenteCotizacion, null);

        Assert.Equal("IA_NO_CONFIGURADA", sinLlave.Code);
        Assert.Equal("IA_APAGADA", apagada.Code);
        Assert.Equal("IA_FUNCION_DESHABILITADA", funcionApagada.Code);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Conversar_con_la_funcion_apagada_falla_antes_de_llamar_al_proveedor()
    {
        var proveedor = new clsProveedorFalso(_ => Texto(["no debía llamarse"]));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() =>
            Correr(Adaptador(proveedor, o => o.Funciones["AsistenteCotizacion"] = false), Peticion()));

        Assert.Equal("IA_FUNCION_DESHABILITADA", ex.Code);
        Assert.Equal(0, proveedor.Llamadas);
    }
}
