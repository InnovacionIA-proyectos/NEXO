using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>Passthrough hacia <c>/tipos-mercancia</c> del Cubicador: qué se transporta, con qué carrocería y bajo qué condiciones; alimenta el desplegable de tipo de mercancía en Captura.</summary>
[ApiController]
[Route("api/tipos-mercancia")]
public class clsTiposMercanciaController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "tipos-mercancia";

    /// <summary>Lista los tipos de mercancía del Cubicador.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    /// <summary>Crea un tipo de mercancía en el Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost]
    public async Task<ActionResult<JsonNode?>> Crear([FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync(Ruta, body.ConUsuario(usuarioActual), ct));

    /// <summary>Actualiza un tipo de mercancía del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{id}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string id, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct));

    /// <summary>Elimina un tipo de mercancía del Cubicador. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(string id, [FromBody] JsonObject body, CancellationToken ct = default)
    {
        await cubicador.DeleteAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct);
        return NoContent();
    }

    /// <summary>Reparación manual de la proyección de NEXO; ver <see cref="clsTiposCarroceriaController.RepublicarCatalogo"/>.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost("republicar-catalogo")]
    public async Task<ActionResult<JsonNode?>> RepublicarCatalogo(CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync($"{Ruta}/republicar-catalogo", new JsonObject(), ct));
}
