using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Biblioteca;

/// <summary>Matriz oficial SICE-TAC leída en vivo (solo lectura) del Data Warehouse (DWSice.dbo.tblRutasSicetac).</summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sicetac")]
public class clsBibliotecaSicetacController(
    IQueryHandler<clsBuscarMatrizSicetacQuery, clsMatrizSicetacResultDto> buscar,
    IQueryHandler<clsPeriodosSicetacQuery, IReadOnlyList<int>> periodos,
    IQueryHandler<clsFiltrosSicetacQuery, clsSicetacFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta la matriz de un periodo (por defecto el más reciente), con filtros por texto de ruta, configuración y condición de carga.
    /// Paginada: <c>page</c> (base 1, default 1) y <c>top</c> como tamaño de página; <c>total</c> es el total sin paginar.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosPesados)]
    public async Task<ActionResult<clsMatrizSicetacResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? periodo,
        [FromQuery] string? configuracion,
        [FromQuery] string? tipoCarga,
        [FromQuery] string? unidadTransporte,
        [FromQuery] string? condicionCarga,
        [FromQuery] string? viaEstandar,
        [FromQuery, Range(1, 500)] int top = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new clsBuscarMatrizSicetacQuery(q, periodo, configuracion, tipoCarga, unidadTransporte, condicionCarga, viaEstandar, top, page), ct));

    /// <summary>Periodos disponibles en el DW (los 12 más recientes).</summary>
    [HttpGet("periodos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<int>>> Periodos(CancellationToken ct = default)
        => Ok(await periodos.Handle(new clsPeriodosSicetacQuery(), ct));

    /// <summary>Valores disponibles para los filtros por columna (periodo más reciente).</summary>
    [HttpGet("filtros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsSicetacFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new clsFiltrosSicetacQuery(), ct));
}
