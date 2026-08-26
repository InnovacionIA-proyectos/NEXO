using CabinaFletes.Application.CasosDeUso.Trabajos;
using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Trabajos;

/// <summary>
/// Estado de los trabajos que corren en segundo plano.
///
/// Es la consulta de respaldo del contrato de integración. Cualquier rol autenticado
/// puede consultarla: preguntar por el avance de un trabajo no revela nada que la
/// operación que lo lanzó no autorizara ya.
/// </summary>
[ApiController]
[Authorize]
[Route("api/trabajos")]
public class TrabajosController(IQueryHandler<ConsultarTrabajoQuery, EstadoTrabajoDto> consultar)
    : ControllerBase
{
    /// <summary>Estado y avance de un trabajo por su identificador.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstadoTrabajoDto>> Get(Guid id, CancellationToken ct = default)
        => Ok(await consultar.Handle(new ConsultarTrabajoQuery(id), ct));
}
