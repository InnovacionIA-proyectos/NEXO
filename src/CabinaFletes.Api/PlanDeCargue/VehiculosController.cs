using CabinaFletes.Application.CasosDeUso.PlanDeCargue;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Mensajes.Cubicacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
/// </summary>
[ApiController]
[Route("api/vehiculos")]
public class VehiculosController(ICommandHandler<GuardarVehiculoCommand, ComandoAceptadoDto> guardar)
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
}
