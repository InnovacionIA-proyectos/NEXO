using System.Text.Json.Nodes;

namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// S5-HU-18.6/#278: passthrough hacia los seis CRUD de catálogo del Cubicador
/// (<c>parametros-motor</c>, <c>categorias-mercancia</c>, <c>tipos-carroceria</c>,
/// <c>tipos-mercancia</c>, <c>vehiculos-catalogo</c>, <c>reglas-incompatibilidad</c> —
/// cubicador/CONTRATO.md §3.2). El portal nunca llama directo al Cubicador: no hay CORS,
/// la llave terminaría en el bundle y el rol no se podría aplicar (ese Bearer autoriza un
/// backend, no una persona). Seis controllers delgados en Api espejan las rutas y aplican
/// el rol; este puerto es el único punto de salida HTTP, deliberadamente "tonto" — no
/// conoce el shape de ningún catálogo, solo reenvía JSON. Por eso NO es
/// <c>JsonElement</c> en los métodos de escritura: <see cref="JsonObject"/> es mutable, así
/// el controller puede estampar <c>usuarioId</c> (el del usuario autenticado, nunca el que
/// mande el cliente) antes de reenviar, sin que el puerto tenga que entender el resto del
/// cuerpo.
/// </summary>
public interface ICubicadorCatalogoPort
{
    Task<JsonNode?> GetAsync(string ruta, CancellationToken ct = default);
    Task<JsonNode?> PostAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);
    Task<JsonNode?> PutAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);

    /// <summary>
    /// DELETE con body (<c>{ usuarioId, version }</c>) — borrar una fila que cambió desde
    /// que se leyó es tan destructivo como pisarla, así que también exige concurrencia
    /// optimista. <c>HttpClient.DeleteAsync</c> no manda body; el adaptador arma el
    /// <c>HttpRequestMessage</c> a mano.
    /// </summary>
    Task DeleteAsync(string ruta, JsonObject cuerpo, CancellationToken ct = default);
}
