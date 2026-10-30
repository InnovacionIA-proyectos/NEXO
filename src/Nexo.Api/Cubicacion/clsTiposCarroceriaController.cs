using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>
/// S5-HU-18.6/#278: passthrough hacia <c>/tipos-carroceria</c> del Cubicador
/// (cubicador/CONTRATO.md §3.2) — el catálogo de carrocerías y qué vehículos las admiten.
/// Incluye <c>republicar-catalogo</c>, que re-emite el evento de creación para TODAS las
/// filas (S5-HU-18.5/#277) — reparación manual de la proyección de NEXO.
/// </summary>
[ApiController]
[Route("api/tipos-carroceria")]
public class clsTiposCarroceriaController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "tipos-carroceria";

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost]
    public async Task<ActionResult<JsonNode?>> Crear([FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync(Ruta, body.ConUsuario(usuarioActual), ct));

    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string id, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct));

    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(string id, [FromBody] JsonObject body, CancellationToken ct = default)
    {
        await cubicador.DeleteAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct);
        return NoContent();
    }

    /// <summary>Reparación manual de la proyección de NEXO (S5-HU-18.5/#277): re-emite el
    /// evento de creación para TODAS las filas, no solo las nuevas. Idempotente (el
    /// consumer hace upsert por Id) — se puede correr más de una vez sin riesgo.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost("republicar-catalogo")]
    public async Task<ActionResult<JsonNode?>> RepublicarCatalogo(CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync($"{Ruta}/republicar-catalogo", new JsonObject(), ct));
}
