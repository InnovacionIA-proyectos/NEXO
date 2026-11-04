using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>Passthrough hacia <c>/vehiculos-catalogo</c> del Cubicador: configuraciones con medidas, capacidad y carrocerías para el motor de selección de flota (no es <c>/api/planes-cargue/vehiculos</c>). Vehículo y carrocerías viajan en un solo payload.</summary>
[ApiController]
[Route("api/vehiculos-catalogo")]
public class clsVehiculosCatalogoController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "vehiculos-catalogo";

    /// <summary>Lista los vehículos del catálogo del motor de selección de flota.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    /// <summary>Crea un vehículo del catálogo en el Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost]
    public async Task<ActionResult<JsonNode?>> Crear([FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync(Ruta, body.ConUsuario(usuarioActual), ct));

    /// <summary>Actualiza un vehículo del catálogo del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string id, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct));

    /// <summary>Elimina un vehículo del catálogo del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(string id, [FromBody] JsonObject body, CancellationToken ct = default)
    {
        await cubicador.DeleteAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct);
        return NoContent();
    }

    /// <summary>Reparación manual de la proyección; ver <see cref="clsTiposCarroceriaController.RepublicarCatalogo"/>. Hoy NEXO no proyecta este catálogo: el endpoint existe por simetría.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost("republicar-catalogo")]
    public async Task<ActionResult<JsonNode?>> RepublicarCatalogo(CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync($"{Ruta}/republicar-catalogo", new JsonObject(), ct));
}
