using Nexo.Application.CasosDeUso.Trabajos;
using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Trabajos;

/// <summary>Estado de los trabajos en segundo plano: consulta de respaldo del contrato de integración, abierta a cualquier rol autenticado.</summary>
[ApiController]
[Authorize]
[Route("api/trabajos")]
public class clsTrabajosController(IQueryHandler<clsConsultarTrabajoQuery, clsEstadoTrabajoDto> consultar)
    : ControllerBase
{
    /// <summary>Estado y avance de un trabajo por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsEstadoTrabajoDto>> Get(Guid id, CancellationToken ct = default)
        => Ok(await consultar.Handle(new clsConsultarTrabajoQuery(id), ct));
}
