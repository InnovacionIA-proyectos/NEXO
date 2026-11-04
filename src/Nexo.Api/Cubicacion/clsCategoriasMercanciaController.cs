using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>Passthrough hacia <c>/categorias-mercancia</c> del Cubicador: las familias que agrupan las mercancías y sobre las que se declaran las incompatibilidades.</summary>
[ApiController]
[Route("api/categorias-mercancia")]
public class clsCategoriasMercanciaController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "categorias-mercancia";

    /// <summary>Lista las categorías de mercancía del Cubicador.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    /// <summary>Crea una categoría de mercancía en el Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost]
    public async Task<ActionResult<JsonNode?>> Crear([FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync(Ruta, body.ConUsuario(usuarioActual), ct));

    /// <summary>Actualiza una categoría de mercancía del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string id, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct));

    /// <summary>Elimina una categoría de mercancía del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(string id, [FromBody] JsonObject body, CancellationToken ct = default)
    {
        await cubicador.DeleteAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct);
        return NoContent();
    }
}
