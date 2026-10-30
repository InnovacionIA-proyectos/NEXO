using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>
/// S5-HU-18.6/#278: passthrough hacia <c>/vehiculos-catalogo</c> del Cubicador
/// (cubicador/CONTRATO.md §3.2) — las configuraciones con sus medidas, capacidad y
/// carrocerías, para el motor de selección de flota. NO es <c>/api/planes-cargue/vehiculos</c>
/// (el catálogo propio del motor 3D) — son dos tablas distintas del lado del Cubicador.
/// El vehículo y sus carrocerías viajan en UN solo payload; el Cubicador reconcilia.
/// </summary>
[ApiController]
[Route("api/vehiculos-catalogo")]
public class clsVehiculosCatalogoController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "vehiculos-catalogo";

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

    /// <summary>Reparación manual de la proyección de NEXO — ver
    /// <see cref="clsTiposCarroceriaController.RepublicarCatalogo"/>. Vehículos del motor de
    /// selección de flota: hoy NEXO no proyecta este catálogo (distinto del de
    /// VehiculosCubicacion/motor 3D), se deja el endpoint por simetría con los otros cinco.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost("republicar-catalogo")]
    public async Task<ActionResult<JsonNode?>> RepublicarCatalogo(CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync($"{Ruta}/republicar-catalogo", new JsonObject(), ct));
}
