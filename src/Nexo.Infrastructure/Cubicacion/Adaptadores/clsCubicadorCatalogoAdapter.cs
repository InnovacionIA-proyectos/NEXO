using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>Espejo de <c>{ "codigo": "...", "mensaje": "..." }</c> (CONTRATO.md §4 del Cubicador).</summary>
internal sealed record clsErrorCatalogoCubicadorDto(
    [property: JsonPropertyName("codigo")] string? Codigo,
    [property: JsonPropertyName("mensaje")] string? Mensaje);

/// <summary>
/// S5-HU-18.6/#278: cliente HTTP del passthrough hacia los seis CRUD de catálogo —
/// ver <see cref="ICubicadorCatalogoPort"/> para el porqué del diseño "tonto" (JSON sin
/// tipar). Usa <c>Cubicador3D:OutboundApiKey</c> (el Bearer genérico que
/// <c>verificar_api_key</c> valida del lado del Cubicador para estos seis endpoints —
/// DISTINTO de <c>Cubicador3D:RecomendacionesApiKey</c>, que es solo para
/// <c>/recomendaciones</c> y exige una llave por sistema).
///
/// Traducción de errores: el código REAL que manda el Cubicador se propaga tal cual
/// (mismo criterio que <c>clsCubicacionRapidaAdapter</c>) — 409 CONFLICTO_DE_VERSION
/// especialmente: su mensaje ya está escrito para mostrarse al usuario del portal
/// ("El registro cambió desde que se cargó. Volver a leerlo y reintentar.").
/// </summary>
public sealed class clsCubicadorCatalogoAdapter(HttpClient http, ILogger<clsCubicadorCatalogoAdapter> log)
    : ICubicadorCatalogoPort
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<JsonNode?> GetAsync(string ruta, CancellationToken ct = default) =>
        EnviarAsync(() => http.GetAsync(ruta, ct), ct);

    public Task<JsonNode?> PostAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default) =>
        EnviarAsync(() => http.PostAsJsonAsync(ruta, cuerpo, Json, ct), ct);

    public Task<JsonNode?> PutAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default) =>
        EnviarAsync(() => http.PutAsJsonAsync(ruta, cuerpo, Json, ct), ct);

    public async Task DeleteAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default)
    {
        // HttpClient.DeleteAsync no manda body: se arma el mensaje a mano (mismo patrón
        // documentado en la Issue #278).
        await EnviarAsync(() =>
        {
            var peticion = new HttpRequestMessage(HttpMethod.Delete, ruta) { Content = JsonContent.Create(cuerpo, options: Json) };
            return http.SendAsync(peticion, ct);
        }, ct);
    }

    private async Task<JsonNode?> EnviarAsync(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await enviar();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Cubicador no respondió al passthrough de catálogo.");
            throw new clsServiceUnavailableException(
                "CUBICADOR_NO_DISPONIBLE", $"El Cubicador no respondió: {ex.Message}");
        }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                if (respuesta.StatusCode == HttpStatusCode.NoContent) return null;
                var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
                if (cuerpo.Length == 0) return null;
                return await JsonNode.ParseAsync(cuerpo, cancellationToken: ct);
            }

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            var error = TryParseError(detalle);
            var codigo = error?.Codigo;
            var mensaje = error?.Mensaje ?? Recortar(detalle);

            throw respuesta.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity =>
                    new clsValidationFailedException(codigo ?? "CATALOGO_INVALIDO", mensaje),
                HttpStatusCode.NotFound =>
                    new clsNotFoundException(codigo ?? "NO_ENCONTRADO", mensaje),
                HttpStatusCode.Conflict =>
                    new clsConflictException(codigo ?? "CONFLICTO_DE_DATOS", mensaje),
                HttpStatusCode.Unauthorized =>
                    new clsServiceUnavailableException(
                        "CUBICADOR_CREDENCIAL_INVALIDA",
                        "Cubicador rechazó el Bearer configurado en Cubicador3D:OutboundApiKey. ¿Está desactualizado tras una rotación?"),
                _ => LogYServiceUnavailable(respuesta.StatusCode, mensaje),
            };
        }
    }

    private Exception LogYServiceUnavailable(HttpStatusCode statusCode, string mensaje)
    {
        log.LogWarning("Cubicador respondió {StatusCode} al passthrough de catálogo: {Detalle}", (int)statusCode, mensaje);
        return new clsServiceUnavailableException("CUBICADOR_ERROR", $"Cubicador respondió {(int)statusCode}: {mensaje}");
    }

    private static clsErrorCatalogoCubicadorDto? TryParseError(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            return JsonSerializer.Deserialize<clsErrorCatalogoCubicadorDto>(cuerpo, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Recortar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? "(sin cuerpo)"
        : texto.Length <= 400 ? texto.Trim()
        : texto[..400].Trim() + "…";
}
