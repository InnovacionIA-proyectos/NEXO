using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.RangosPeso;
using Nexo.Application.CasosDeUso.RangosPeso.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.RangosPeso;

/// <summary>Catálogo propio de bandas de peso (kg) para Tarifas de Plaza (S4-HU-10.1) —
/// no es un catálogo cerrado, se pueden agregar bandas nuevas con el tiempo.</summary>
[ApiController]
[Authorize(Roles = "AdminTarifas,Admin")]
[Route("api/pesos")]
public class clsRangosPesoController(
    IQueryHandler<clsListarRangosPesoQuery, IReadOnlyList<clsRangoPesoDto>> listar,
    ICommandHandler<clsCrearRangoPesoCommand, clsRangoPesoDto> crear,
    ICommandHandler<clsEditarRangoPesoCommand, clsRangoPesoDto> editar,
    ICommandHandler<clsEliminarRangoPesoCommand, bool> eliminar)
    : ControllerBase
{
    /// <summary>Lista todas las bandas de peso.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<IReadOnlyList<clsRangoPesoDto>>> Listar(CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarRangosPesoQuery(), ct));

    /// <summary>Crea una banda de peso nueva.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsRangoPesoDto>> Crear([FromBody] clsRangoPesoRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearRangoPesoCommand(req.PesoMinimo, req.PesoMaximo), ct);
        return Created($"/api/pesos/{dto.Id}", dto);
    }

    /// <summary>Edita una banda de peso existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsRangoPesoDto>> Editar(int id, [FromBody] clsRangoPesoRequest req, CancellationToken ct = default)
        => Ok(await editar.Handle(new clsEditarRangoPesoCommand(id, req.PesoMinimo, req.PesoMaximo), ct));

    /// <summary>Elimina una banda de peso por su id.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct = default)
    {
        if (!await eliminar.Handle(new clsEliminarRangoPesoCommand(id), ct))
            throw new clsNotFoundException("RANGO_PESO_NO_ENCONTRADO", "No existe una banda de peso con ese identificador.");
        return NoContent();
    }
}

public class clsRangoPesoRequest
{
    public int PesoMinimo { get; set; }
    public int? PesoMaximo { get; set; }
}
