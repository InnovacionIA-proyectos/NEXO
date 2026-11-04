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
    // Valores documentados en cubicador/CONTRATO.md. Sin [RegularExpression] un valor inválido llegaba al validador del mensaje y volvía con nombres internos de mensajería en vez de VALIDATION_ERROR.
    // En un record con constructor primario la metadata de validación va sobre el parámetro (sin "property:"): con "property:" ASP.NET Core la ignora y falla con 500.
    [Required]
    [RegularExpression("(?i)^(trasera|frontal|costado_izquierdo|costado_derecho)$",
        ErrorMessage = "puertaPosicion debe ser trasera, frontal, costado_izquierdo o costado_derecho.")]
    string PuertaPosicion,
    IReadOnlyList<clsEjeVehiculoRequest> Ejes,
    decimal ToleranciaParedesMm = 0,
    decimal ToleranciaEntreUnidadesMm = 0);

/// <summary>
/// Alta de vehículos/contenedores por cola: NEXO solo genera el id y publica; la fuente de verdad del vehículo vive en el Cubicador. La lectura (GET) se resuelve en NEXO contra la proyección local.
/// </summary>
[ApiController]
[Route("api/vehiculos")]
public class clsVehiculosController(
    ICommandHandler<clsGuardarVehiculoCommand, clsComandoAceptadoDto> guardar,
    ICommandHandler<clsEliminarVehiculoCommand, clsComandoAceptadoDto> eliminar,
    IQueryHandler<clsListarVehiculosQuery, clsPaginaDto<clsVehiculoCubicacionDto>> listar)
    : ControllerBase
{
    /// <summary>Da de alta un vehículo del catálogo de cubicación. Responde 202: se publica por cola y la fuente de verdad es el Cubicador.</summary>
    [Authorize(Roles = "Comercial,AdminTarifas,Admin")]
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
    /// Baja de un vehículo del catálogo, solo Admin (el catálogo es maestro y lo comparten todos los planes). 409 VEHICULO_EN_USO si algún plan de cargue aún lo referencia;
    /// si no, publica el comando y responde 202.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsComandoAceptadoDto>> Eliminar(Guid id, CancellationToken ct = default)
        => Accepted(await eliminar.Handle(new clsEliminarVehiculoCommand(id), ct));
    /// <summary>Catálogo contra la proyección local, paginado (page/pageSize).</summary>
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
