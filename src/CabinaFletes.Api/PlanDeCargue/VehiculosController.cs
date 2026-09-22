using System.ComponentModel.DataAnnotations;
using CabinaFletes.Api.Common.RateLimiting;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Mensajes.Cubicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CabinaFletes.Api.PlanDeCargue;

public record EjeVehiculoRequest(decimal PosicionMm, decimal CapacidadKg);

public record GuardarVehiculoRequest(
    string Nombre,
    string? Descripcion,
    decimal LargoInternoMm,
    decimal AnchoInternoMm,
    decimal AltoInternoMm,
    decimal CapacidadKg,
    decimal TaraKg,
    decimal PuertaAnchoMm,
    decimal PuertaAltoMm,
    string PuertaPosicion,
    IReadOnlyList<EjeVehiculoRequest> Ejes,
    decimal ToleranciaParedesMm = 0,
    decimal ToleranciaEntreUnidadesMm = 0);

/// <summary>
/// S3-HU-02 (issue #104): alta de vehículos/contenedores por cola — reemplaza el POST
/// síncrono que el navegador hacía directo contra Cubicador. La fuente de verdad del
/// vehículo (dimensiones, presets, catálogo) sigue viviendo en Cubicador; NEXO solo
/// genera el id y publica (ver issue #108 para el detalle de esta asimetría intencional).
///
/// S3-HU-03 (issue #112) agrega la lectura: GET, a diferencia del POST, SÍ está resuelto
/// enteramente en NEXO — contra la proyección local que vehiculo.creado/vehiculo.actualizado
/// mantienen sincronizada (issue #110/#111), nunca contra Cubicador en caliente.
/// </summary>
[ApiController]
[Route("api/vehiculos")]
public class VehiculosController(
    ICommandHandler<GuardarVehiculoCommand, ComandoAceptadoDto> guardar,
    ICommandHandler<EliminarVehiculoCommand, ComandoAceptadoDto> eliminar,
    IQueryHandler<ListarVehiculosQuery, PaginaDto<VehiculoCubicacionDto>> listar)
    : ControllerBase
{
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ComandoAceptadoDto>> Guardar(
        [FromBody] GuardarVehiculoRequest body, CancellationToken ct = default)
    {
        var r = await guardar.Handle(new GuardarVehiculoCommand(
            body.Nombre, body.Descripcion, body.LargoInternoMm, body.AnchoInternoMm, body.AltoInternoMm,
            body.CapacidadKg, body.TaraKg, body.PuertaAnchoMm, body.PuertaAltoMm, body.PuertaPosicion,
            body.Ejes.Select(e => new EjeVehiculoInput(e.PosicionMm, e.CapacidadKg)).ToList(),
            body.ToleranciaParedesMm, body.ToleranciaEntreUnidadesMm), ct);
        return Accepted(r);
    }

    /// <summary>
    /// Baja de un vehículo del catálogo. Rol Admin: el catálogo es maestro y lo comparten
    /// todos los planes, a diferencia del alta, que un comercial puede necesitar en medio
    /// de una cotización.
    ///
    /// Issue #155: responde 409 VEHICULO_EN_USO si algún plan de cargue todavía referencia
    /// el vehículo (validado síncrono contra la fila espejo de PlanCargue, ver
    /// EliminarVehiculoCommandHandler). Solo si no hay ningún plan asociado se publica el
    /// comando y responde 202.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminar.Handle(new EliminarVehiculoCommand(id), ct));
    /// <summary>S3-HU-03 (issue #112): catálogo contra la proyección local.
    /// [Authorize] sin rol específico — mismo criterio que el resto de las consultas
    /// del sistema (docs/ROLES_Y_PERMISOS.md). Issue #166: pagina (page/pageSize,
    /// mismo patrón que /api/servicios-conexos) aunque el catálogo hoy sea chico.</summary>
    [Authorize]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(RateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<PaginaDto<VehiculoCubicacionDto>>> Listar(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await listar.Handle(new ListarVehiculosQuery(page, pageSize), ct));
}
