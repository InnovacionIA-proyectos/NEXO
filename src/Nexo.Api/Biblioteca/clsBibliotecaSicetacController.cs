using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.Biblioteca;

/// <summary>
/// Biblioteca — matriz oficial SICE-TAC leída en vivo (SOLO LECTURA) del Data
/// Warehouse corporativo (DWSice.dbo.tblRutasSicetac).
/// </summary>
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
    /// Consulta la matriz del periodo indicado (por defecto el más reciente),
    /// con filtros por texto de ruta, configuración y condición de carga.
    ///
    /// Medido en vivo, la matriz tiene ~2,15 M de filas por periodo filtrado y
    /// "top" nunca dejaba ver más allá de las primeras 50-200, sin aviso de
    /// que el resto existía aunque `total` ya viajaba en la respuesta. "page"
    /// (1-based, opcional, default 1) agrega el `Skip` que faltaba; "top" sigue
    /// siendo el tamaño de página. Cambio aditivo: mismo shape de respuesta de
    /// siempre (`{ periodo, total, items }`).
    /// </summary>
    [HttpGet]
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
    public async Task<ActionResult<IReadOnlyList<int>>> Periodos(CancellationToken ct = default)
        => Ok(await periodos.Handle(new clsPeriodosSicetacQuery(), ct));

    /// <summary>Valores disponibles para los filtros por columna (periodo más reciente).</summary>
    [HttpGet("filtros")]
    public async Task<ActionResult<clsSicetacFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new clsFiltrosSicetacQuery(), ct));
}
