using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Biblioteca;

/// <summary>Propuesta de tarifa por línea: el valor más alto entre el flete de plaza SAP y el piso SICE-TAC (el histórico SAP va de referencia).</summary>
[ApiController]
[Authorize(Roles = "Comercial,AdminTarifas,ServiciosLogisticos,LiderProceso,Admin")] // Es la Mesa de Costeo: AdminBiblioteca no entra
[Route("api/solicitudes/{id:guid}/propuesta-tarifa")]
public class clsPropuestaTarifaController(IQueryHandler<clsProponerPropuestaTarifaQuery, clsPropuestaSolicitudDto> proponer)
    : ControllerBase
{
    /// <summary>Calcula la propuesta para cada línea de la solicitud. No persiste nada.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsPropuestaSolicitudDto>> Get(Guid id, CancellationToken ct = default)
        => Ok(await proponer.Handle(new clsProponerPropuestaTarifaQuery(id), ct));
}
