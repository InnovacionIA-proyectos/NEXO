using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Biblioteca;

/// <summary>
/// Propuesta de tarifa por línea a partir de la Biblioteca: compara el flete de
/// plaza SAP contra el piso SICE-TAC (y el histórico SAP como referencia) y
/// propone el valor más alto.
/// </summary>
[ApiController]
[Authorize]
[Route("api/solicitudes/{id:guid}/propuesta-tarifa")]
public class clsPropuestaTarifaController(IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto> proponer)
    : ControllerBase
{
    /// <summary>Calcula la propuesta para cada línea de la solicitud. No persiste nada.</summary>
    [HttpGet]
    public async Task<ActionResult<clsPropuestaSolicitudDto>> Get(Guid id, CancellationToken ct = default)
        => Ok(await proponer.Handle(new clsProponerPropuestaTarifaQuery(id), ct));
}
