using Nexo.Application.CasosDeUso.IA;
using Nexo.Application.CasosDeUso.IA.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.IA;

/// <summary>Uso y gasto de los modelos de lenguaje. Solo lectura y solo para el administrador.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/ia/uso")]
public class clsIaUsoController(IQueryHandler<clsConsultarGastoIaQuery, IReadOnlyList<clsGastoIaDto>> gasto) : ControllerBase
{
    /// <summary>
    /// Gasto por mes (calendario de Colombia) y por función: llamadas, errores, tokens, costo estimado y latencia promedio. Sin <c>desde</c> mira seis meses; <c>funcion</c> filtra por una.
    /// El costo es una estimación con precios de lista y suma solo las llamadas con precio conocido (<c>llamadasSinCosto</c> dice cuántas faltan).
    /// </summary>
    [HttpGet("gasto")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<clsGastoIaDto>>> Gasto(
        [FromQuery] DateTimeOffset? desde, [FromQuery] DateTimeOffset? hasta, [FromQuery] string? funcion, CancellationToken ct = default)
        => Ok(await gasto.Handle(new clsConsultarGastoIaQuery(desde, hasta, funcion), ct));
}
