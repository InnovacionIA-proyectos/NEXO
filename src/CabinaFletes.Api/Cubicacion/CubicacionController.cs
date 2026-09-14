using CabinaFletes.Application.CasosDeUso.Cubicacion;
using CabinaFletes.Application.CasosDeUso.Cubicacion.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Cubicacion;

public record ItemCubicacionRequest(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm,
    decimal? PesoKg, string? TipoMercancia)
{
    public ItemCubicacionRapidaDto ADto() => new(Descripcion, Cantidad, LargoCm, AnchoCm, AltoCm, PesoKg, TipoMercancia);
}

public record CubicarRequest(IReadOnlyList<ItemCubicacionRequest> Items, IReadOnlyList<string>? UnidadIds, string? ClaseViaje)
{
    public CubicacionRapidaSolicitud ASolicitud() => new(Items.Select(i => i.ADto()).ToList(), UnidadIds, ClaseViaje);
}

/// <summary>
/// [S3-HU-04.3, issue #78] Reemplaza el POST que <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>, usado por <c>captura-page</c> y <c>mesa-costeo-page</c>)
/// hacía directo contra Cubicador con la clave compartida. Mismos roles que el resto de
/// Cubicación porque las dos pantallas que lo usan pertenecen a Comercial (captura) y a
/// Coordinación de Fletes (mesa de costeo).
/// </summary>
[ApiController]
[Route("api/cubicacion")]
public class CubicacionController(IQueryHandler<CubicarRapidoQuery, CubicacionRapidaResultado> cubicar) : ControllerBase
{
    /// <summary>
    /// Cubicación rápida sin persistencia: dado un conjunto de ítems, devuelve de una vez
    /// las matrices de evaluación por unidad y la sugerencia de vehículo/flota — igual que
    /// hoy responde Cubicador, solo que por NEXO. Ver <see cref="Application.CasosDeUso.Cubicacion.Interfaces.ICubicacionRapidaPort"/>
    /// para por qué esta es la única llamada HTTP saliente hacia Cubicador del sistema.
    /// </summary>
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CubicacionRapidaResultado>> Cubicar([FromBody] CubicarRequest body, CancellationToken ct = default)
        => Ok(await cubicar.Handle(new CubicarRapidoQuery(body.ASolicitud()), ct));
}
