using Nexo.Application.CasosDeUso.Cubicacion;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

public record clsItemCubicacionRequest(
    string? Descripcion, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm,
    decimal? PesoKg, string? TipoMercancia)
{
    public clsItemCubicacionRapidaDto ADto() => new(Descripcion, Cantidad, LargoCm, AnchoCm, AltoCm, PesoKg, TipoMercancia);
}

/// <summary>
/// "Items" es obligatorio en negocio (400 ITEMS_REQUERIDOS si falta o viene vacío) pero
/// se tipa como <c>IReadOnlyList&lt;...&gt;?</c> y no sin "?" a propósito: con Nullable
/// Reference Types habilitado, [ApiController] trata un parámetro de body no-nullable
/// como implícitamente [Required] y responde su propio 400 VALIDATION_ERROR genérico (en
/// inglés, sin pasar por el método) antes de llegar a la validación explícita de abajo —
/// mismo problema ya documentado y revertido en <see cref="Api.Solicitudes.clsCapturaSolicitudesController.Aprovisionamiento"/>.
/// La validación explícita del controller se mantiene por eso.
/// </summary>
public record clsCubicarRequest(IReadOnlyList<clsItemCubicacionRequest>? Items, IReadOnlyList<string>? UnidadIds, string? ClaseViaje)
{
    public clsCubicacionRapidaSolicitud ASolicitud() => new(Items!.Select(i => i.ADto()).ToList(), UnidadIds, ClaseViaje);
}

/// <summary>
/// Reemplaza el POST que <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>, usado por <c>captura-page</c> y <c>mesa-costeo-page</c>)
/// hacía directo contra Cubicador con la clave compartida. Mismos roles que el resto de
/// Cubicación porque las dos pantallas que lo usan pertenecen a Comercial (captura) y a
/// Coordinación de Fletes (mesa de costeo).
/// </summary>
[ApiController]
[Route("api/cubicacion")]
public class clsCubicacionController(IQueryHandler<clsCubicarRapidoQuery, clsCubicacionRapidaResultado> cubicar) : ControllerBase
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
    public async Task<ActionResult<clsCubicacionRapidaResultado>> Cubicar([FromBody] clsCubicarRequest body, CancellationToken ct = default)
    {
        if (body.Items is null || body.Items.Count == 0)
            throw new clsValidationFailedException("ITEMS_REQUERIDOS", "Debe indicar al menos un ítem para cubicar.");

        return Ok(await cubicar.Handle(new clsCubicarRapidoQuery(body.ASolicitud()), ct));
    }
}
