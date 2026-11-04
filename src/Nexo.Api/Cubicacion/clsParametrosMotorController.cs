using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>Passthrough hacia <c>/parametros-motor</c> del Cubicador: parámetros fijos del motor (<c>k_max</c>, <c>factor_vol_util</c>, <c>margen_cg</c>…); se editan, no se crean ni se borran.</summary>
[ApiController]
[Route("api/parametros-motor")]
public class clsParametrosMotorController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "parametros-motor";

    /// <summary>Lista los parámetros del motor de cubicación.</summary>
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    /// <summary>Actualiza el valor de un parámetro del motor por su clave. Solo AdminTarifas y Admin.</summary>
    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{clave}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string clave, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{clave}", body.ConUsuario(usuarioActual), ct));
}
