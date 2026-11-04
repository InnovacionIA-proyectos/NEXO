using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>Espejo de <c>{ "codigo": "...", "mensaje": "..." }</c> del Cubicador.</summary>
internal sealed record clsErrorCubicadorDto(
    [property: JsonPropertyName("codigo")] string? Codigo,
    [property: JsonPropertyName("mensaje")] string? Mensaje);

/// <summary>Cliente HTTP saliente hacia Cubicador (excepción al "solo colas", ver <see cref="ICubicacionRapidaPort"/>).
/// Usa el Bearer de Cubicador3D:RecomendacionesApiKey y JSON camelCase (coincide con los DTOs sin mapeo).
/// Propaga el mismo código de error del Cubicador; SIN_SELECCION_AUTOMATICA y condicionada llegan con 200 y no son errores.</summary>
public sealed class clsCubicacionRapidaAdapter(HttpClient http, ILogger<clsCubicacionRapidaAdapter> log)
    : ICubicacionRapidaPort
{
    private const string clsRuta = "recomendaciones";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<clsRecomendacionDto> CrearAsync(clsCrearRecomendacionInput solicitud, CancellationToken ct = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.PostAsJsonAsync(clsRuta, solicitud, Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Cubicador no respondió a la creación de recomendación.");
            throw new clsServiceUnavailableException(
                "CUBICADOR_NO_DISPONIBLE", $"El Cubicador no respondió: {ex.Message}");
        }

        return await LeerRespuestaAsync(respuesta, esConsulta: false, ct);
    }

    public async Task<clsRecomendacionDto> ConsultarAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default)
    {
        var query = $"{clsRuta}?idLineaSolicitud={Uri.EscapeDataString(idLineaSolicitud)}&origenSistema={Uri.EscapeDataString(origenSistema)}";
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.GetAsync(query, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Cubicador no respondió a la reconsulta de recomendación.");
            throw new clsServiceUnavailableException(
                "CUBICADOR_NO_DISPONIBLE", $"El Cubicador no respondió: {ex.Message}");
        }

        return await LeerRespuestaAsync(respuesta, esConsulta: true, ct);
    }

    private async Task<clsRecomendacionDto> LeerRespuestaAsync(HttpResponseMessage respuesta, bool esConsulta, CancellationToken ct)
    {
        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
                var resultado = await JsonSerializer.DeserializeAsync<clsRecomendacionDto>(cuerpo, Json, ct);
                return resultado ?? throw new clsServiceUnavailableException(
                    "CUBICADOR_RESPUESTA_VACIA", "El Cubicador respondió 2xx sin cuerpo utilizable.");
            }

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);
            var error = TryParseError(detalle);

            // 400: la culpa es del request, no del transporte; se propaga el código REAL del Cubicador
            // para distinguir p. ej. MERCANCIA_DESCONOCIDA de DIMENSIONES_INVALIDAS.
            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
                throw new clsValidationFailedException(
                    error?.Codigo ?? "CUBICACION_INVALIDA", error?.Mensaje ?? Recortar(detalle));

            // "Sin recomendación" es una respuesta del Cubicador (404 con código propio), no una caída; solo aplica a la RECONSULTA:
            // un 404 al CREAR es una ruta mal configurada y sí es 503.
            if (esConsulta && respuesta.StatusCode == HttpStatusCode.NotFound)
                throw new clsNotFoundException(
                    "RECOMENDACION_NO_ENCONTRADA",
                    error?.Mensaje ?? "El Cubicador no tiene una recomendación para esta línea.");

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                throw new clsServiceUnavailableException(
                    "CUBICADOR_CREDENCIAL_INVALIDA",
                    "Cubicador rechazó el Bearer configurado en Cubicador3D:RecomendacionesApiKey. ¿Está desactualizado tras una rotación?");

            log.LogWarning(
                "Cubicador respondió {StatusCode} a /recomendaciones: {Detalle}",
                (int)respuesta.StatusCode, Recortar(detalle));
            throw new clsServiceUnavailableException(
                "CUBICADOR_ERROR", $"Cubicador respondió {(int)respuesta.StatusCode}: {error?.Mensaje ?? Recortar(detalle)}");
        }
    }

    private static clsErrorCubicadorDto? TryParseError(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo)) return null;
        try
        {
            return JsonSerializer.Deserialize<clsErrorCubicadorDto>(cuerpo, Json);
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
