using System.Text.Json.Nodes;

namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// Passthrough hacia los seis CRUD de catálogo del Cubicador. El portal nunca llama directo (la llave terminaría en el bundle y el rol no se aplicaría);
/// este puerto solo reenvía JSON. Usa <see cref="JsonObject"/> (mutable) para que el controller estampe el <c>usuarioId</c> autenticado antes de reenviar.
/// </summary>
public interface ICubicadorCatalogoPort
{
    Task<JsonNode?> GetAsync(string ruta, CancellationToken ct = default);
    Task<JsonNode?> PostAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);
    Task<JsonNode?> PutAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);

    /// <summary>
    /// DELETE con body (<c>{ usuarioId, version }</c>): exige concurrencia optimista igual que escribir. <c>HttpClient.DeleteAsync</c> no manda body, el adaptador arma el request a mano.
    /// </summary>
    Task DeleteAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);
}
