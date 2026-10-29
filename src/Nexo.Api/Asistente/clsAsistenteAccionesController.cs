using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Asistente;

/// <summary>
/// Los cambios que el asistente propone. El asistente nunca escribe: deja la acción pendiente y la persona la confirma aquí, con su
/// identidad y su rol, y se aplica con los mismos casos de uso de siempre (las reglas del dominio mandan).
/// </summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/asistente/acciones")]
public class clsAsistenteAccionesController(
    ICommandHandler<clsConfirmarAccionAsistenteCommand, clsAccionAsistenteDto> confirmar,
    ICommandHandler<clsCancelarAccionAsistenteCommand, clsAccionAsistenteDto> cancelar) : ControllerBase
{
    /// <summary>
    /// Aplica el cambio propuesto. Solo quien tiene la conversación, una sola vez y antes de que venza. Si una regla del dominio lo rechaza
    /// responde ese mismo error (por ejemplo <c>409 FLETE_SOLO_SICETAC</c>, <c>409 COTIZACION_NO_EDITABLE</c>, <c>400 FLETE_BAJO_PISO_SICETAC</c>) y
    /// no se modifica nada; la acción queda <c>fallida</c> y la conversación lo explica.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAccionAsistenteDto>> Confirmar(Guid id, CancellationToken ct = default)
        => Ok(await confirmar.Handle(new clsConfirmarAccionAsistenteCommand(id), ct));

    /// <summary>Descarta el cambio propuesto sin aplicarlo.</summary>
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAccionAsistenteDto>> Cancelar(Guid id, CancellationToken ct = default)
        => Ok(await cancelar.Handle(new clsCancelarAccionAsistenteCommand(id), ct));
}
