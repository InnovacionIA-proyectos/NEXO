using System.Diagnostics;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Infrastructure.IA;

/// <summary>
/// Salida hacia Claude por el SDK oficial de Anthropic para .NET.
///
/// SIN LLAVE O CON LA FUNCIÓN APAGADA el adaptador existe igual (la aplicación arranca) pero no crea el cliente ni llama a la
/// red: <see cref="Habilitada"/> devuelve false y <see cref="CompletarAsync"/> lanza un 503 con código estable.
///
/// LO QUE SE ENVÍA es solo lo de <see cref="clsPeticionIa"/>: instrucciones, mensajes, esfuerzo, tope de tokens y, si se pide,
/// el formato de salida. No se envían <c>temperature</c>, <c>top_p</c> ni <c>top_k</c> (la API los rechaza). Con
/// <see cref="clsOpcionesAnthropic.RazonamientoAdaptativo"/> el modelo razona según el esfuerzo.
///
/// ERRORES. Los del proveedor se traducen a <see cref="clsServiceUnavailableException"/> con un código de
/// <see cref="IAsistenteIaPort"/>. Ni los mensajes ni los logs llevan la llave ni el contenido de la petición: se registra
/// función, modelo, tokens, latencia y resultado. Los reintentos de 429, 5xx y errores de red los hace el SDK
/// (<see cref="clsOpcionesAnthropic.MaxReintentos"/>).
/// </summary>
public sealed class clsAnthropicAdapter : IAsistenteIaPort
{
    private readonly clsOpcionesAnthropic _opciones;
    private readonly ILogger<clsAnthropicAdapter> _log;
    private readonly AnthropicClient? _cliente;

    /// <param name="http">Cliente HTTP propio del SDK; solo lo usan las pruebas para simular al proveedor.</param>
    /// <param name="urlBase">Dirección del proveedor; solo la usan las pruebas junto con <paramref name="http"/>.</param>
    public clsAnthropicAdapter(
        IOptions<clsOpcionesAnthropic> opciones, ILogger<clsAnthropicAdapter> log, HttpClient? http = null, string? urlBase = null)
    {
        _opciones = opciones.Value;
        _log = log;
        if (!_opciones.Configurada) return;

        var llave = _opciones.ApiKey!.Trim();
        _cliente = http is null
            ? new AnthropicClient { ApiKey = llave, MaxRetries = _opciones.ReintentosEfectivos, Timeout = _opciones.TimeoutEfectivo }
            : new AnthropicClient
            {
                ApiKey = llave, MaxRetries = _opciones.ReintentosEfectivos, Timeout = _opciones.TimeoutEfectivo,
                HttpClient = http, BaseUrl = urlBase ?? "https://api.anthropic.com",
            };
    }

    public bool Habilitada(clsFuncionIa funcion) => _opciones.Configurada && _opciones.FuncionEncendida(funcion);

    public async Task<clsRespuestaIa> CompletarAsync(clsPeticionIa peticion, CancellationToken ct = default)
    {
        if (!_opciones.Configurada || _cliente is null)
            throw new clsServiceUnavailableException("IA_NO_CONFIGURADA", "La función de IA no está configurada en este ambiente.");
        if (!_opciones.FuncionEncendida(peticion.Funcion))
            throw new clsServiceUnavailableException("IA_FUNCION_DESHABILITADA", "La función de IA está apagada en este ambiente.");

        var parametros = Armar(peticion);
        var reloj = Stopwatch.StartNew();
        Message respuesta;
        try
        {
            respuesta = await _cliente.Messages.Create(parametros, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var (codigo, mensaje) = Traducir(ex);
            _log.LogWarning("IA {Funcion} falló: {Codigo} ({Tipo}, {Estado}) tras {Ms} ms. Proveedor: {Detalle}",
                peticion.Funcion, codigo, ex.GetType().Name, (ex as AnthropicApiException)?.StatusCode, reloj.ElapsedMilliseconds,
                DetalleDelProveedor(ex));
            throw new clsServiceUnavailableException(codigo, mensaje);
        }
        reloj.Stop();

        var texto = string.Concat(respuesta.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        var tokensEntrada = respuesta.Usage.InputTokens;
        var tokensSalida = respuesta.Usage.OutputTokens;

        string? json = null;
        string? falla = null;
        if (respuesta.StopReason == "refusal")
            falla = "IA_RECHAZADA";
        else if (respuesta.StopReason == "max_tokens")
            falla = "IA_RESPUESTA_TRUNCADA";
        else if (peticion.EsquemaSalidaJson is not null)
        {
            if (EsJsonValido(texto)) json = texto;
            else falla = "IA_RESPUESTA_INVALIDA";
        }

        _log.LogInformation(
            "IA {Funcion} modelo {Modelo}: {Entrada} tokens de entrada, {Salida} de salida, {Ms} ms, {Resultado}",
            peticion.Funcion, respuesta.Model, tokensEntrada, tokensSalida, reloj.ElapsedMilliseconds, falla ?? "ok");

        if (falla is not null)
            throw new clsServiceUnavailableException(falla, MensajeDe(falla));

        return new clsRespuestaIa(texto, json, respuesta.Model.Raw(), tokensEntrada, tokensSalida, reloj.Elapsed);
    }

    private MessageCreateParams Armar(clsPeticionIa p)
    {
        var salida = new OutputConfig { Effort = EsfuerzoDe(_opciones.Effort) };
        if (p.EsquemaSalidaJson is not null)
        {
            var esquema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(p.EsquemaSalidaJson)
                ?? throw new ArgumentException("El esquema de salida no es un objeto JSON.", nameof(p));
            salida = new OutputConfig { Effort = EsfuerzoDe(_opciones.Effort), Format = new JsonOutputFormat { Schema = esquema } };
        }

        var mensajes = p.Mensajes.Select(m => string.Equals(m.Rol, clsMensajeIa.RolAsistente, StringComparison.OrdinalIgnoreCase)
            ? (MessageParam)new AssistantMessageParam { Content = m.Contenido }
            : new UserMessageParam { Content = m.Contenido }).ToList();

        return _opciones.RazonamientoAdaptativo
            ? new MessageCreateParams
            {
                Model = _opciones.ModeloEfectivo, MaxTokens = p.MaxTokensSalida ?? _opciones.MaxTokensEfectivo,
                System = p.Instrucciones, Messages = mensajes, OutputConfig = salida, Thinking = new ThinkingConfigAdaptive(),
                WorkspaceID = _opciones.WorkspaceIdEfectivo,
            }
            : new MessageCreateParams
            {
                Model = _opciones.ModeloEfectivo, MaxTokens = p.MaxTokensSalida ?? _opciones.MaxTokensEfectivo,
                System = p.Instrucciones, Messages = mensajes, OutputConfig = salida,
                WorkspaceID = _opciones.WorkspaceIdEfectivo,
            };
    }

    private static Effort EsfuerzoDe(string? valor) => valor?.Trim().ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "xhigh" => Effort.Xhigh,
        "max" => Effort.Max,
        _ => Effort.High,
    };

    private static bool EsJsonValido(string texto)
    {
        try { using var _ = JsonDocument.Parse(texto); return true; }
        catch (JsonException) { return false; }
    }

    /// <summary>
    /// Lo que respondió el proveedor ante un error (tipo, mensaje e id de la petición), para poder diagnosticar un rechazo sin
    /// reproducirlo. Es el texto de diagnóstico del proveedor, no la petición: no incluye los mensajes enviados. Se recorta y,
    /// por si acaso, se borra la llave.
    /// </summary>
    private string DetalleDelProveedor(Exception ex)
    {
        if (ex is not AnthropicApiException) return ex.GetType().Name;
        var texto = ex.Message.ReplaceLineEndings(" ");
        if (_opciones.Configurada) texto = texto.Replace(_opciones.ApiKey!.Trim(), "<llave>");
        return texto.Length <= 500 ? texto : texto[..500];
    }

    /// <summary>Un tiempo agotado puede llegar envuelto (error de red del SDK con una cancelación por dentro).</summary>
    private static bool EsTiempoAgotado(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is TimeoutException or OperationCanceledException) return true;
        return false;
    }

    private static (string Codigo, string Mensaje) Traducir(Exception ex) => ex switch
    {
        _ when EsTiempoAgotado(ex) => ("IA_TIEMPO_AGOTADO", "El proveedor de IA no respondió a tiempo."),
        AnthropicRateLimitException => ("IA_LIMITE_PROVEEDOR", "El proveedor de IA alcanzó su límite de uso; intenta de nuevo en unos minutos."),
        AnthropicUnauthorizedException or AnthropicForbiddenException =>
            ("IA_CREDENCIAL_INVALIDA", "La credencial del proveedor de IA no es válida o no tiene permiso."),
        Anthropic5xxException or AnthropicIOException =>
            ("IA_NO_DISPONIBLE", "El proveedor de IA no está disponible en este momento."),
        AnthropicBadRequestException or AnthropicUnprocessableEntityException =>
            ("IA_PETICION_RECHAZADA", "El proveedor de IA rechazó la petición."),
        AnthropicNotFoundException =>
            ("IA_PETICION_RECHAZADA", "El proveedor de IA no encontró lo pedido; revisa el modelo configurado."),
        _ => ("IA_NO_DISPONIBLE", "El proveedor de IA no pudo atender la petición."),
    };

    private static string MensajeDe(string codigo) => codigo switch
    {
        "IA_RECHAZADA" => "El modelo rechazó la petición.",
        "IA_RESPUESTA_TRUNCADA" => "La respuesta del modelo quedó incompleta.",
        _ => "La respuesta del modelo no tiene el formato esperado.",
    };
}
