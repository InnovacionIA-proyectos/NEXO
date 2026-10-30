using System.Text.Json.Nodes;
using Nexo.Application.CasosDeUso.Cubicacion.Interfaces;
using Nexo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

/// <summary>
/// S5-HU-18.6/#278: passthrough hacia <c>/parametros-motor</c> del Cubicador
/// (cubicador/CONTRATO.md §3.2) — <c>k_max</c>, <c>umbral_alerta_llenado</c>,
/// <c>usar_alternativa</c>, <c>factor_vol_util</c>, <c>margen_cg</c>. Sin <c>POST</c>/
/// <c>DELETE</c>: son parámetros fijos del motor, se editan, no se crean ni se borran.
/// </summary>
[ApiController]
[Route("api/parametros-motor")]
public class clsParametrosMotorController(ICubicadorCatalogoPort cubicador, ICurrentUserPort usuarioActual) : ControllerBase
{
    private const string Ruta = "parametros-motor";

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<JsonNode?>> Listar(CancellationToken ct = default) => Ok(await cubicador.GetAsync(Ruta, ct));

    [Authorize(Roles = "AdminTarifas,Admin")]
    [HttpPut("{clave}")]
    public async Task<ActionResult<JsonNode?>> Actualizar(string clave, [FromBody] JsonObject body, CancellationToken ct = default) =>
        Ok(await cubicador.PutAsync($"{Ruta}/{clave}", body.ConUsuario(usuarioActual), ct));
}
