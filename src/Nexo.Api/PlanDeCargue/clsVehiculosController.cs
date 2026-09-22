using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Mensajes.Cubicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.PlanDeCargue;

public record clsEjeVehiculoRequest(decimal PosicionMm, decimal CapacidadKg);

public record clsGuardarVehiculoRequest(
    string Nombre,
    string? Descripcion,
    decimal LargoInternoMm,
    decimal AnchoInternoMm,
    decimal AltoInternoMm,
    decimal CapacidadKg,
    decimal TaraKg,
    decimal PuertaAnchoMm,
    decimal PuertaAltoMm,
    // Valores documentados en cubicador/CONTRATO.md junto a VehiculoCubicacionDto.
    // Sin este [RegularExpression], un valor inválido pasaba de largo hasta el
    // validador del mensaje a publicar (clsCubicacionSolicitadaValidador) y volvía
    // como un 400 que mencionaba "clsCubicacionSolicitada"/"clsSolicitud" — nombres
    // internos de mensajería, no del contrato HTTP — en vez de VALIDATION_ERROR.
    [property: Required]
    [property: RegularExpression("(?i)^(trasera|frontal|costado_izquierdo|costado_derecho)$",
        ErrorMessage = "puertaPosicion debe ser trasera, frontal, costado_izquierdo o costado_derecho.")]
    string PuertaPosicion,
    IReadOnlyList<clsEjeVehiculoRequest> Ejes,
    decimal ToleranciaParedesMm = 0,
    decimal ToleranciaEntreUnidadesMm = 0);

/// <summary>
/// Alta de vehículos/contenedores por cola — reemplaza el POST síncrono que el
/// navegador hacía directo contra Cubicador. La fuente de verdad del vehículo
/// (dimensiones, presets, catálogo) sigue viviendo en Cubicador; NEXO solo genera
/// el id y publica, una asimetría intencional.
///
/// La lectura (GET), a diferencia del POST, SÍ está resuelta enteramente en NEXO —
/// contra la proyección local que vehiculo.creado/vehiculo.actualizado mantienen
/// sincronizada, nunca contra Cubicador en caliente.
/// </summary>
[ApiController]
[Route("api/vehiculos")]
public class clsVehiculosController(
    ICommandHandler<clsGuardarVehiculoCommand, clsComandoAceptadoDto> guardar,
    ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto> eliminar,
    IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>> listar)
    : ControllerBase
{
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsComandoAceptadoDto>> Guardar(
        [FromBody] clsGuardarVehiculoRequest body, CancellationToken ct = default)
    {
        var r = await guardar.Handle(new clsGuardarVehiculoCommand(
            body.Nombre, body.Descripcion, body.LargoInternoMm, body.AnchoInternoMm, body.AltoInternoMm,
            body.CapacidadKg, body.TaraKg, body.PuertaAnchoMm, body.PuertaAltoMm,
            body.PuertaPosicion.Trim().ToLowerInvariant(),
            body.Ejes.Select(e => new clsEjeVehiculoInput(e.PosicionMm, e.CapacidadKg)).ToList(),
            body.ToleranciaParedesMm, body.ToleranciaEntreUnidadesMm), ct);
        return Accepted(r);
    }

    /// <summary>
    /// Baja de un vehículo del catálogo. Rol Admin: el catálogo es maestro y lo comparten
    /// todos los planes, a diferencia del alta, que un comercial puede necesitar en medio
    /// de una cotización.
    ///
    /// Responde 409 VEHICULO_EN_USO si algún plan de cargue todavía referencia el
    /// vehículo (validado síncrono contra la fila espejo de clsPlanCargue, ver
    /// clsEliminarVehiculoCommandHandler). Solo si no hay ningún plan asociado se publica el
    /// comando y responde 202.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminar.Handle(new clsEliminarVehiculoCommand(id), ct));
    /// <summary>Catálogo contra la proyección local. [Authorize] sin rol específico —
    /// mismo criterio que el resto de las consultas del sistema
    /// (docs/ROLES_Y_PERMISOS.md). Pagina (page/pageSize, mismo patrón que
    /// /api/servicios-conexos) aunque el catálogo hoy sea chico.</summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsPaginaDto<clsVehiculoCubicacionDto>>> Listar(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarVehiculosQuery(page, pageSize), ct));
}
