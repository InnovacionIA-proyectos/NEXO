using System.Text.Json;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Asistente;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Asistente;

/// <summary>
/// Herramientas de lectura del asistente de cotización. Son las mismas consultas que ya expone NEXO, envueltas para que el asistente las
/// pida por nombre; se ejecutan con la identidad de quien llama, así que nunca muestran más de lo que su rol ya puede ver. No escriben.
/// </summary>
[ApiController]
[Authorize]
[Route("api/asistente/herramientas")]
public class clsAsistenteHerramientasController(
    IQueryHandler<clsListarHerramientasAsistenteQuery, IReadOnlyList<clsDefinicionHerramientaDto>> listar,
    IQueryHandler<clsEjecutarHerramientaAsistenteQuery, clsResultadoHerramienta> ejecutar) : ControllerBase
{
    /// <summary>Las herramientas disponibles para el rol del usuario, con la forma de su entrada (JSON Schema).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsDefinicionHerramientaDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarHerramientasAsistenteQuery(), ct));

    /// <summary>
    /// Ejecuta una herramienta. El cuerpo es la entrada de la herramienta (un objeto JSON). La respuesta trae siempre la <c>fuente</c>;
    /// sin datos, <c>encontrado</c> es false y <c>nota</c> dice por qué. Errores: 400 <c>HERRAMIENTA_DESCONOCIDA</c> o
    /// <c>HERRAMIENTA_ENTRADA_INVALIDA</c>, 403 <c>HERRAMIENTA_NO_PERMITIDA</c> (o <c>SOLICITUD_AJENA</c>), 404 si la solicitud o la línea
    /// no existen.
    /// </summary>
    [HttpPost("{nombre}")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsResultadoHerramienta>> Ejecutar(string nombre, [FromBody] JsonElement entrada, CancellationToken ct = default)
        => Ok(await ejecutar.Handle(new clsEjecutarHerramientaAsistenteQuery(nombre, entrada), ct));
}
