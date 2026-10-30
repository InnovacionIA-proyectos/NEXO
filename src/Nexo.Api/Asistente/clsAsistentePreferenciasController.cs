using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Asistente;

/// <summary>
/// Las reglas y preferencias propias de quien usa el asistente. Se guardan conversando (el asistente las propone y la persona las confirma);
/// aquí se ven y se borran. Son de cada persona: nadie ve ni borra las de otra.
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/asistente/preferencias")]
public class clsAsistentePreferenciasController(
    IQueryHandler<clsListarPreferenciasAsistenteQuery, IReadOnlyList<clsPreferenciaAsistenteDto>> listar,
    ICommandHandler<clsEliminarPreferenciaAsistenteCommand, Unit> eliminar) : ControllerBase
{
    /// <summary>Las preferencias de quien consulta, de la más antigua a la más reciente.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<clsPreferenciaAsistenteDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarPreferenciasAsistenteQuery(), ct));

    /// <summary>Borra una preferencia propia; el asistente deja de aplicarla desde la siguiente respuesta.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken ct = default)
    {
        await eliminar.Handle(new clsEliminarPreferenciaAsistenteCommand(id), ct);
        return NoContent();
    }
}
