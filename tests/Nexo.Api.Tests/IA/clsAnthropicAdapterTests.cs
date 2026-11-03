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
/// El adaptador de Claude contra un proveedor SIMULADO (un manejador HTTP que responde lo programado): ninguna prueba sale a la
/// red ni usa una llave real. Lo que se verifica es lo que el adaptador ENVÍA (modelo, esfuerzo, formato de salida y la ausencia
/// de temperature/top_p/top_k) y cómo traduce lo que el proveedor responde.
/// </summary>
public class clsAnthropicAdapterTests
{
    private const string Llave = "sk-ant-LLAVE-FALSA-DE-PRUEBA";

    private sealed class clsProveedorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private int _llamadas;
        public List<string> Cuerpos { get; } = [];
        public List<string?> Autorizaciones { get; } = [];
        public List<string?> Workspaces { get; } = [];
        public int Llamadas => _llamadas;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Cuerpos.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            Autorizaciones.Add(request.Headers.Authorization?.ToString());
            Workspaces.Add(request.Headers.TryGetValues("anthropic-workspace-id", out var w) ? w.FirstOrDefault() : null);
            return responder(Interlocked.Increment(ref _llamadas));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode estado, string json) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Mensaje(
        string texto, string motivo = "end_turn", int entrada = 12, int salida = 5, string modelo = "claude-sonnet-5-5",
        int cacheLectura = 0, int cacheEscritura = 0) =>
        Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id = "msg_prueba", type = "message", role = "assistant", model = modelo,
            content = new[] { new { type = "text", text = texto } },
            stop_reason = motivo, stop_sequence = (string?)null,
            usage = new
            {
                input_tokens = entrada, output_tokens = salida,
                cache_read_input_tokens = cacheLectura, cache_creation_input_tokens = cacheEscritura,
            },
        }));

    private static HttpResponseMessage Error(HttpStatusCode estado, string tipo) =>
        Json(estado, JsonSerializer.Serialize(new { type = "error", error = new { type = tipo, message = "detalle del proveedor" } }));

    private static clsAnthropicAdapter Adaptador(
        clsProveedorFalso proveedor, Action<clsOpcionesAnthropic>? ajustar = null, bool conLlave = true)
    {
        var opciones = new clsOpcionesAnthropic
        {
            ApiKey = conLlave ? Llave : null,
            MaxReintentos = 0,
            Funciones = new(StringComparer.OrdinalIgnoreCase) { ["ImportacionExcel"] = true },
        };
        ajustar?.Invoke(opciones);
        return new clsAnthropicAdapter(
            Options.Create(opciones), NullLogger<clsAnthropicAdapter>.Instance, new HttpClient(proveedor), "http://proveedor.prueba");
    }

    private static clsPeticionIa Peticion(string? esquema = null, clsFuncionIa funcion = clsFuncionIa.ImportacionExcel) => new(
        funcion, "Eres un asistente de prueba.",
        [new clsMensajeIa(clsMensajeIa.RolUsuario, "Hola"), new clsMensajeIa(clsMensajeIa.RolAsistente, "Dime."), new clsMensajeIa(clsMensajeIa.RolUsuario, "Mapea esto")],
        esquema);

    private const string EsquemaDePrueba = """{"type":"object","properties":{"campo":{"type":"string"}},"required":["campo"],"additionalProperties":false}""";

    // ------------------------------------------------------------ lo que se envía

    [Fact]
    public async Task Envia_modelo_esfuerzo_instrucciones_y_mensajes_y_devuelve_texto_tokens_y_modelo()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("hola", entrada: 12, salida: 5));

        var r = await Adaptador(proveedor).CompletarAsync(Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        var raiz = cuerpo.RootElement;
        Assert.Equal("claude-sonnet-5-5", raiz.GetProperty("model").GetString());
        // `medium` desde #363: lo recomienda la evaluacion del mapeo de Excel. Ver clsOpcionesAnthropic.
        Assert.Equal("medium", raiz.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("adaptive", raiz.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal(4096, raiz.GetProperty("max_tokens").GetInt32());
        Assert.Contains("Eres un asistente de prueba.", raiz.GetProperty("system").ToString());
        var mensajes = raiz.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["user", "assistant", "user"], mensajes.Select(m => m.GetProperty("role").GetString()));

        Assert.Equal("hola", r.Texto);
        Assert.Null(r.JsonEstructurado);
        Assert.Equal(12, r.TokensEntrada);
        Assert.Equal(5, r.TokensSalida);
        Assert.Equal("claude-sonnet-5-5", r.Modelo);
        // El SDK envía la llave como Authorization: Bearer (la API la acepta igual que en X-Api-Key).
        Assert.Equal($"Bearer {Llave}", Assert.Single(proveedor.Autorizaciones));
    }

    [Fact]
    public async Task Con_workspace_configurada_la_envia_en_la_cabecera_y_sin_ella_no_envia_nada()
    {
        var con = new clsProveedorFalso(_ => Mensaje("ok"));
        await Adaptador(con, o => o.WorkspaceId = "  wrkspc_prueba123 ").CompletarAsync(Peticion());
        Assert.Equal("wrkspc_prueba123", Assert.Single(con.Workspaces));

        var sin = new clsProveedorFalso(_ => Mensaje("ok"));
        await Adaptador(sin).CompletarAsync(Peticion());
        Assert.Null(Assert.Single(sin.Workspaces));
    }

    [Fact]
    public async Task Nunca_envia_temperature_top_p_ni_top_k()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("{\"campo\":\"x\"}"));

        await Adaptador(proveedor).CompletarAsync(Peticion(EsquemaDePrueba));

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        foreach (var prohibido in new[] { "temperature", "top_p", "top_k" })
            Assert.False(cuerpo.RootElement.TryGetProperty(prohibido, out _), $"El cuerpo no debe llevar {prohibido}.");
    }

    [Fact]
    public async Task El_modelo_y_el_esfuerzo_salen_de_la_configuracion()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok", modelo: "claude-sonnet-4-6"));

        await Adaptador(proveedor, o => { o.Model = "claude-sonnet-4-6"; o.Effort = "MEDIUM"; o.MaxTokensSalida = 2000; })
            .CompletarAsync(Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        Assert.Equal("claude-sonnet-4-6", cuerpo.RootElement.GetProperty("model").GetString());
        Assert.Equal("medium", cuerpo.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal(2000, cuerpo.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task Un_esfuerzo_desconocido_cae_al_esfuerzo_por_defecto()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));

        await Adaptador(proveedor, o => o.Effort = "muchisimo").CompletarAsync(Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        Assert.Equal("high", cuerpo.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task Sin_razonamiento_adaptativo_no_envia_thinking()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));

        await Adaptador(proveedor, o => o.RazonamientoAdaptativo = false).CompletarAsync(Peticion());

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        Assert.False(cuerpo.RootElement.TryGetProperty("thinking", out _));
    }

    // ------------------------------------------------------------ salida estructurada

    [Fact]
    public async Task Con_esquema_pide_el_formato_json_y_devuelve_el_json_validado()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("{\"campo\":\"valor\"}"));

        var r = await Adaptador(proveedor).CompletarAsync(Peticion(EsquemaDePrueba));

        using var cuerpo = JsonDocument.Parse(Assert.Single(proveedor.Cuerpos));
        var formato = cuerpo.RootElement.GetProperty("output_config").GetProperty("format");
        Assert.Equal("json_schema", formato.GetProperty("type").GetString());
        Assert.Equal("object", formato.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("{\"campo\":\"valor\"}", r.JsonEstructurado);
    }

    [Fact]
    public async Task Con_esquema_una_respuesta_que_no_es_json_se_rechaza()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("esto no es json"));

        var ex = await Assert.ThrowsAnyAsync<clsServiceUnavailableException>(() => Adaptador(proveedor).CompletarAsync(Peticion(EsquemaDePrueba)));

        Assert.Equal("IA_RESPUESTA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Un_esquema_que_no_es_json_es_un_error_de_programacion_y_no_llama_al_proveedor()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));

        await Assert.ThrowsAnyAsync<JsonException>(() => Adaptador(proveedor).CompletarAsync(Peticion("no es un esquema")));

        Assert.Equal(0, proveedor.Llamadas);
    }

    // ------------------------------------------------------------ cómo termina el modelo

    [Fact]
    public async Task Un_rechazo_del_modelo_se_traduce_a_IA_RECHAZADA()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("", motivo: "refusal"));

        var ex = await Assert.ThrowsAnyAsync<clsServiceUnavailableException>(() => Adaptador(proveedor).CompletarAsync(Peticion()));

        Assert.Equal("IA_RECHAZADA", ex.Code);
    }

    [Fact]
    public async Task Una_respuesta_cortada_por_el_tope_de_tokens_se_traduce_a_IA_RESPUESTA_TRUNCADA()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("{\"campo\":\"cort", motivo: "max_tokens"));

        var ex = await Assert.ThrowsAnyAsync<clsServiceUnavailableException>(() => Adaptador(proveedor).CompletarAsync(Peticion(EsquemaDePrueba)));

        Assert.Equal("IA_RESPUESTA_TRUNCADA", ex.Code);
    }

    // ------------------------------------------------------------ errores del proveedor

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "authentication_error", "IA_CREDENCIAL_INVALIDA")]
    [InlineData(HttpStatusCode.Forbidden, "permission_error", "IA_CREDENCIAL_INVALIDA")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error", "IA_LIMITE_PROVEEDOR")]
    [InlineData(HttpStatusCode.InternalServerError, "api_error", "IA_NO_DISPONIBLE")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "overloaded_error", "IA_NO_DISPONIBLE")]
    [InlineData(HttpStatusCode.BadRequest, "invalid_request_error", "IA_PETICION_RECHAZADA")]
    [InlineData(HttpStatusCode.NotFound, "not_found_error", "IA_PETICION_RECHAZADA")]
    public async Task Un_error_del_proveedor_se_traduce_a_un_503_con_codigo_estable(HttpStatusCode estado, string tipo, string codigo)
    {
        var proveedor = new clsProveedorFalso(_ => Error(estado, tipo));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Adaptador(proveedor).CompletarAsync(Peticion()));

        Assert.Equal(codigo, ex.Code);
        Assert.DoesNotContain(Llave, ex.Message);
        Assert.DoesNotContain("Mapea esto", ex.Message);
    }

    [Fact]
    public async Task Un_proveedor_que_no_responde_se_traduce_a_IA_TIEMPO_AGOTADO()
    {
        var proveedor = new clsProveedorFalso(_ => throw new TaskCanceledException("tiempo de espera simulado"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Adaptador(proveedor).CompletarAsync(Peticion()));

        Assert.Equal("IA_TIEMPO_AGOTADO", ex.Code);
    }

    [Fact]
    public async Task Si_quien_llama_cancela_la_cancelacion_se_propaga_y_no_se_disfraza_de_503()
    {
        using var cancelar = new CancellationTokenSource();
        var proveedor = new clsProveedorFalso(_ => { cancelar.Cancel(); throw new TaskCanceledException(); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Adaptador(proveedor).CompletarAsync(Peticion(), cancelar.Token));
    }

    [Fact]
    public async Task Con_reintentos_configurados_un_error_pasajero_se_reintenta_y_se_recupera()
    {
        var proveedor = new clsProveedorFalso(n => n == 1 ? Error(HttpStatusCode.InternalServerError, "api_error") : Mensaje("recuperado"));

        var r = await Adaptador(proveedor, o => o.MaxReintentos = 1).CompletarAsync(Peticion());

        Assert.Equal("recuperado", r.Texto);
        Assert.Equal(2, proveedor.Llamadas);
    }

    [Fact]
    public async Task Los_tokens_de_cache_de_la_respuesta_se_leen()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok", entrada: 100, salida: 20, cacheLectura: 1500, cacheEscritura: 300));

        var r = await Adaptador(proveedor).CompletarAsync(Peticion());

        Assert.Equal((100, 20, 1500, 300), (r.TokensEntrada, r.TokensSalida, r.TokensCacheLectura, r.TokensCacheEscritura));
    }

    [Fact]
    public async Task Una_respuesta_que_no_sirve_lleva_los_tokens_gastados_para_poder_registrarlos()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("esto no es json", entrada: 321, salida: 45));

        var ex = await Assert.ThrowsAsync<clsIaRespuestaNoUtilException>(() => Adaptador(proveedor).CompletarAsync(Peticion(EsquemaDePrueba)));

        Assert.Equal("IA_RESPUESTA_INVALIDA", ex.Code);
        Assert.Equal((321, 45), (ex.Uso.Entrada, ex.Uso.Salida));
        Assert.Equal("claude-sonnet-5-5", ex.Uso.Modelo);
    }

    // ------------------------------------------------------------ sin llave / función apagada

    [Fact]
    public async Task Con_el_interruptor_general_apagado_ninguna_funcion_esta_habilitada_y_responde_IA_APAGADA()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));
        var adaptador = Adaptador(proveedor, o => o.Habilitada = false);

        Assert.All(Enum.GetValues<clsFuncionIa>(), f => Assert.False(adaptador.Habilitada(f)));
        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CompletarAsync(Peticion()));

        Assert.Equal("IA_APAGADA", ex.Code);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Sin_llave_no_esta_habilitada_y_responde_IA_NO_CONFIGURADA_sin_llamar_al_proveedor()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));
        var adaptador = Adaptador(proveedor, conLlave: false);

        Assert.All(Enum.GetValues<clsFuncionIa>(), f => Assert.False(adaptador.Habilitada(f)));
        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => adaptador.CompletarAsync(Peticion()));

        Assert.Equal("IA_NO_CONFIGURADA", ex.Code);
        Assert.Equal(0, proveedor.Llamadas);
    }

    [Fact]
    public async Task Con_la_funcion_apagada_responde_IA_FUNCION_DESHABILITADA_sin_llamar_al_proveedor()
    {
        var proveedor = new clsProveedorFalso(_ => Mensaje("ok"));
        var adaptador = Adaptador(proveedor);

        Assert.True(adaptador.Habilitada(clsFuncionIa.ImportacionExcel));
        Assert.False(adaptador.Habilitada(clsFuncionIa.AsistenteCotizacion));
        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(
            () => adaptador.CompletarAsync(Peticion(funcion: clsFuncionIa.AsistenteCotizacion)));

        Assert.Equal("IA_FUNCION_DESHABILITADA", ex.Code);
        Assert.Equal(0, proveedor.Llamadas);
    }
}
