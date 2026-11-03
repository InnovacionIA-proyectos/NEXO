using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>Espejo de <c>{ "explicacion": "..." | null }</c> del Cubicador.</summary>
internal sealed record clsExplicacionCubicadorDto([property: JsonPropertyName("explicacion")] string? Explicacion);

/// <summary>
/// Llama <c>{BaseAddress}recomendaciones/explicacion</c> con la misma llave dedicada de NEXO que la recomendación
/// (<c>Cubicador3D:RecomendacionesApiKey</c>). Es un cliente aparte del de <see cref="clsCubicacionRapidaAdapter"/> para tener su propio tiempo
/// de espera (<c>Cubicador3D:ExplicacionTimeoutSegundos</c>): el texto lo escribe un modelo y tarda segundos.
/// La explicación es opcional, así que cualquier falla (red, tiempo agotado, llave rechazada, 404 sin recomendación, 5xx, cuerpo ilegible) se
/// registra y devuelve null; la pantalla no la necesita para funcionar.
/// </summary>
public sealed class clsCubicadorExplicacionAdapter(HttpClient http, ILogger<clsCubicadorExplicacionAdapter> log) : ICubicadorExplicacionPort
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<string?> ObtenerAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default)
    {
        var ruta = $"recomendaciones/explicacion?idLineaSolicitud={Uri.EscapeDataString(idLineaSolicitud)}&origenSistema={Uri.EscapeDataString(origenSistema)}";
        try
        {
            using var respuesta = await http.GetAsync(ruta, ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                log.LogWarning("Cubicador respondió {StatusCode} a la explicación de la recomendación; se devuelve sin explicación.", (int)respuesta.StatusCode);
                return null;
            }

            var cuerpo = await respuesta.Content.ReadFromJsonAsync<clsExplicacionCubicadorDto>(Json, ct);
            return string.IsNullOrWhiteSpace(cuerpo?.Explicacion) ? null : cuerpo.Explicacion.Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException && !ct.IsCancellationRequested)
        {
            log.LogWarning(ex, "Cubicador no entregó la explicación de la recomendación; se devuelve sin explicación.");
            return null;
        }
    }
}

/// <summary>Sin <c>Cubicador3D:OutboundBaseUrl</c> o <c>Cubicador3D:RecomendacionesApiKey</c>: no hay explicación (no es un error).</summary>
public sealed class clsCubicadorExplicacionNoConfiguradoAdapter : ICubicadorExplicacionPort
{
    public Task<string?> ObtenerAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default) => Task.FromResult<string?>(null);
}
