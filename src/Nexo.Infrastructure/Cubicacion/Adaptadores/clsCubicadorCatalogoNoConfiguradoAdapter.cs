using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common.Exceptions;

namespace Nexo.Infrastructure.Cubicacion.Adaptadores;

/// <summary>
/// Se registra cuando falta <c>Cubicador3D:OutboundBaseUrl</c> y/o
/// <c>Cubicador3D:OutboundApiKey</c>: los seis passthrough de catálogo (S5-HU-18.6/#278)
/// responden 503 accionable en vez de fallar la inyección de dependencias.
/// </summary>
public sealed class clsCubicadorCatalogoNoConfiguradoAdapter : ICubicadorCatalogoPort
{
    public Task<JsonNode?> GetAsync(string ruta, CancellationToken ct = default) => NoConfigurado<JsonNode?>();
    public Task<JsonNode?> PostAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default) => NoConfigurado<JsonNode?>();
    public Task<JsonNode?> PutAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default) => NoConfigurado<JsonNode?>();
    public Task DeleteAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default) => NoConfigurado<JsonNode?>();

    private static Task<T> NoConfigurado<T>() => throw new clsServiceUnavailableException(
        "CATALOGO_CUBICADOR_NO_CONFIGURADO",
        "El passthrough de catálogo no está configurado. Define Cubicador3D:OutboundBaseUrl y Cubicador3D:OutboundApiKey.");
}
