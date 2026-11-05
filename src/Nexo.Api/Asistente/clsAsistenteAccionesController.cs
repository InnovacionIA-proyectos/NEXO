using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Asistente;

/// <summary>Cambios que propone el asistente: la persona los confirma aquí y se aplican con los casos de uso normales.</summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/asistente/acciones")]
public class clsAsistenteAccionesController(
    ICommandHandler<clsConfirmarAccionAsistenteCommand, clsAccionAsistenteDto> confirmar,
    ICommandHandler<clsCancelarAccionAsistenteCommand, clsAccionAsistenteDto> cancelar) : ControllerBase
{
    /// <summary>
    /// Aplica el cambio propuesto (solo su dueño, una vez y antes de que venza). Si una regla del dominio lo rechaza responde ese error y la acción queda <c>fallida</c>.
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
