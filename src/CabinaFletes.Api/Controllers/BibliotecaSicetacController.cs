using System.ComponentModel.DataAnnotations;
using CabinaFletes.Application.Biblioteca.Dtos;
using CabinaFletes.Application.Biblioteca.Queries;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>
/// Biblioteca — matriz oficial SICE-TAC leída en vivo (SOLO LECTURA) del Data
/// Warehouse corporativo (DWSice.dbo.tblRutasSicetac).
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sicetac")]
public class BibliotecaSicetacController(
    IQueryHandler<BuscarMatrizSicetacQuery, MatrizSicetacResultDto> buscar,
    IQueryHandler<PeriodosSicetacQuery, IReadOnlyList<int>> periodos,
    IQueryHandler<FiltrosSicetacQuery, SicetacFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta la matriz del periodo indicado (por defecto el más reciente),
    /// con filtros por texto de ruta, configuración y condición de carga.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MatrizSicetacResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? periodo,
        [FromQuery] string? configuracion,
        [FromQuery] string? tipoCarga,
        [FromQuery] string? unidadTransporte,
        [FromQuery] string? condicionCarga,
        [FromQuery] string? viaEstandar,
        [FromQuery, Range(1, 500)] int top = 50,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new BuscarMatrizSicetacQuery(q, periodo, configuracion, tipoCarga, unidadTransporte, condicionCarga, viaEstandar, top), ct));

    /// <summary>Periodos disponibles en el DW (los 12 más recientes).</summary>
    [HttpGet("periodos")]
    public async Task<ActionResult<IReadOnlyList<int>>> Periodos(CancellationToken ct = default)
        => Ok(await periodos.Handle(new PeriodosSicetacQuery(), ct));

    /// <summary>Valores disponibles para los filtros por columna (periodo más reciente).</summary>
    [HttpGet("filtros")]
    public async Task<ActionResult<SicetacFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new FiltrosSicetacQuery(), ct));
}
