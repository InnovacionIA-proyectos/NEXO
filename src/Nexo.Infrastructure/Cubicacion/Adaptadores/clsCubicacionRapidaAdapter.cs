using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>Espejo de <c>{ "codigo": "...", "mensaje": "..." }</c> (CONTRATO.md §4 del Cubicador).</summary>
internal sealed record clsErrorCubicadorDto(
    [property: JsonPropertyName("codigo")] string? Codigo,
    [property: JsonPropertyName("mensaje")] string? Mensaje);

/// <summary>
/// Único cliente HTTP saliente de NEXO hacia Cubicador — ver
/// <see cref="ICubicacionRapidaPort"/> para por qué esta operación concreta es una
/// excepción declarada al "solo colas" del resto de Cubicación.
///
/// S5-HU-18.1/#273: llama <c>{BaseAddress}recomendaciones</c> (reemplaza al retirado
/// <c>POST /api/cubicacion</c>) con el Bearer de <c>Cubicador3D:RecomendacionesApiKey</c>
/// — equivalente del lado de NEXO a <c>CUBICADOR_API_KEY_NEXO</c>, distinto del
/// <c>Cubicador3D:OutboundApiKey</c> genérico que usaba el contrato viejo y que el
/// Cubicador ya no acepta en este endpoint.
///
/// JSON en camelCase explícito: <c>cubicador/api/dtos/comun.py::ApiModel</c> usa
/// <c>alias_generator=to_camel</c> "igual que JsonSerializerDefaults.Web de NEXO" (su
/// propio comentario) — así que el shape ya coincide con las propiedades PascalCase de
/// los DTOs de Application sin necesidad de un mapeo campo a campo.
///
/// Traducción de errores: el Cubicador devuelve <c>{ codigo, mensaje }</c> real
/// (CONTRATO.md §4) — se parsea y se propaga EL MISMO código (catálogo existente
/// ampliado: MERCANCIAS_REQUERIDAS, MERCANCIA_DESCONOCIDA, MERCANCIA_NO_CUBICABLE,
/// DIMENSIONES_INVALIDAS, CUBICACION_DEMASIADO_GRANDE), no un código inventado por NEXO
/// que lo reemplace. <c>SIN_SELECCION_AUTOMATICA</c> y <c>condicionada</c> NO son errores:
/// llegan con 200 dentro de <see cref="clsRecomendacionDto.Resultado"/> — este adaptador no
/// les da tratamiento especial, los deja pasar como cualquier respuesta exitosa.
/// </summary>
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

        return await LeerRespuestaAsync(respuesta, ct);
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

        return await LeerRespuestaAsync(respuesta, ct);
    }

    private async Task<clsRecomendacionDto> LeerRespuestaAsync(HttpResponseMessage respuesta, CancellationToken ct)
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

            // 400 (mercancía/medidas/tamaño del request inválidos): la culpa es del request,
            // no del transporte — se propaga el código REAL que manda el Cubicador
            // (catálogo ampliado de la sección 2.6/§4, no uno inventado por NEXO), para que
            // quien llame pueda distinguir MERCANCIA_DESCONOCIDA de DIMENSIONES_INVALIDAS.
            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
                throw new clsValidationFailedException(
                    error?.Codigo ?? "CUBICACION_INVALIDA", error?.Mensaje ?? Recortar(detalle));

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
