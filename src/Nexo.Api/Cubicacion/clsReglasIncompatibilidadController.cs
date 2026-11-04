using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>Passthrough hacia <c>/reglas-incompatibilidad</c> del Cubicador: pares de categorías que no pueden viajar juntos. Sin PUT: una regla se borra y se crea.</summary>
[ApiController]
[Route("api/reglas-incompatibilidad")]
public class clsReglasIncompatibilidadController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "reglas-incompatibilidad";

    /// <summary>Lista las reglas de incompatibilidad entre categorías de mercancía.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    /// <summary>Crea una regla de incompatibilidad entre dos categorías. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPost]
    public async Task<ActionResult<JsonNode?>> Crear([FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PostAsync(Ruta, body.ConUsuario(usuarioActual), ct));

    /// <summary>Elimina una regla de incompatibilidad. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Eliminar(string id, [FromBody] JsonObject body, CancellationToken ct = default)
    {
        await cubicador.DeleteAsync($"{Ruta}/{id}", body.ConUsuario(usuarioActual), ct);
        return NoContent();
    }
}
