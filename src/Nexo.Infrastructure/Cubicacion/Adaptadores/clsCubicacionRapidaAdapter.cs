using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>
/// [S3-HU-04.3, issue #78] Único cliente HTTP saliente de NEXO hacia Cubicador — ver
/// <see cref="ICubicacionRapidaPort"/> para por qué esta operación concreta es una
/// excepción declarada al "solo colas" del resto de Cubicación.
///
/// Llama <c>POST {BaseAddress}api/cubicacion</c> con el mismo Bearer estático que
/// Cubicador valida en <c>verificar_api_key</c> (<c>clsCubicador3D:OutboundApiKey</c>,
/// configurado como header por defecto del <see cref="HttpClient"/> tipado en
/// DependencyInjection — nunca por request, para no repetir el secreto en el código).
///
/// JSON en camelCase explícito: <c>cubicador/api/dtos/comun.py::ApiModel</c> usa
/// <c>alias_generator=to_camel</c> "igual que JsonSerializerDefaults.Web de NEXO" (su
/// propio comentario) — así que el shape ya coincide con las propiedades PascalCase de
/// los DTOs de Application sin necesidad de un mapeo campo a campo.
/// </summary>
public sealed class clsCubicacionRapidaAdapter(HttpClient http, ILogger<clsCubicacionRapidaAdapter> log)
    : ICubicacionRapidaPort
{
    private const string clsRuta = "api/cubicacion";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<clsCubicacionRapidaResultado> EvaluarAsync(clsCubicacionRapidaSolicitud solicitud, CancellationToken ct = default)
    {
        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.PostAsJsonAsync(clsRuta, solicitud, Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Cubicador no respondió a la cubicación rápida.");
            throw new clsServiceUnavailableException(
                "CUBICADOR_NO_DISPONIBLE", $"El Cubicador no respondió: {ex.Message}");
        }

        using (respuesta)
        {
            if (respuesta.IsSuccessStatusCode)
            {
                var cuerpo = await respuesta.Content.ReadAsStreamAsync(ct);
                var resultado = await JsonSerializer.DeserializeAsync<clsCubicacionRapidaResultado>(cuerpo, Json, ct);
                return resultado ?? throw new clsServiceUnavailableException(
                    "CUBICADOR_RESPUESTA_VACIA", "El Cubicador respondió 2xx sin cuerpo utilizable.");
            }

            var detalle = await respuesta.Content.ReadAsStringAsync(ct);

            // 400 (items/medidas inválidas): la culpa es del request, no del transporte — se
            // traduce a validación, no a "servicio no disponible" (no tiene sentido reintentar).
            if (respuesta.StatusCode == HttpStatusCode.BadRequest)
                throw new clsValidationFailedException(
                    "CUBICACION_INVALIDA", $"Cubicador rechazó la solicitud: {Recortar(detalle)}");

            if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
                throw new clsServiceUnavailableException(
                    "CUBICADOR_CREDENCIAL_INVALIDA",
                    "Cubicador rechazó el Bearer configurado en clsCubicador3D:OutboundApiKey. ¿Está desactualizado tras una rotación?");

            log.LogWarning(
                "Cubicador respondió {StatusCode} a la cubicación rápida: {Detalle}",
                (int)respuesta.StatusCode, Recortar(detalle));
            throw new clsServiceUnavailableException(
                "CUBICADOR_ERROR", $"Cubicador respondió {(int)respuesta.StatusCode}: {Recortar(detalle)}");
        }
    }

    private static string Recortar(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? "(sin cuerpo)"
        : texto.Length <= 400 ? texto.Trim()
        : texto[..400].Trim() + "…";
}
