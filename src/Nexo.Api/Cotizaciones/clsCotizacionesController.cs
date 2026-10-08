using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cotizaciones;

// -----------------------------------------------------------------------------
// POLÍTICA DE ACCESO (SEC-03) — igual que en el proyecto original: Nexo es una
// herramienta interna con cola de trabajo compartida; la segmentación es por ROL
// con [Authorize(Roles=...)] en cada endpoint, no por propiedad de instancia.
// -----------------------------------------------------------------------------

/// <summary>
/// Cotización integral de una solicitud: fletes por línea, servicios conexos,
/// entrega al comercial por email y registro del resultado (ganada/perdida).
/// Controller delgado: valida el request HTTP, lo mapea a un Command/Query de
/// Application y devuelve el DTO que el Handler produjo. No conoce EF Core ni
/// ningún detalle de Infrastructure.
/// </summary>
[ApiController]
[Authorize]
[Route("api/cotizaciones")]
public class clsCotizacionesController(
    ICommandHandler<clsCrearCotizacionCommand, clsCotizacionDto> crear,
    ICommandHandler<clsAgregarLineaCotizacionCommand, clsCotizacionDto> agregarLinea,
    ICommandHandler<clsAgregarConexosCotizacionCommand, clsCotizacionDto> agregarConexos,
    ICommandHandler<clsEditarConexoCotizacionCommand, clsCotizacionDto> editarConexo,
    ICommandHandler<clsEliminarConexoCotizacionCommand, clsCotizacionDto> eliminarConexo,
    ICommandHandler<clsAplicarPropuestasCommand, clsAplicarPropuestasResultDto> aplicarPropuestas,
    ICommandHandler<clsEntregarCotizacionCommand, clsEntregarCotizacionResultDto> entregar,
    ICommandHandler<clsMarcarResultadoCommand, clsCotizacionDto> marcarResultado,
    IQueryHandler<clsObtenerCotizacionQuery, clsCotizacionDto> obtener,
    IQueryHandler<clsExportarCotizacionExcelQuery, clsArchivoDto> exportarExcel)
    : ControllerBase
{
    /// <summary>Crear o recuperar la cotización (borrador) de una solicitud.</summary>
    [HttpPost]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsCotizacionDto>> Crear([FromBody] clsCrearCotizacionRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearCotizacionCommand(req.SolicitudId), ct);
        return Created($"/api/cotizaciones/{dto.Id}", dto);
    }

    /// <summary>Consolidado/total de la cotización integral.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsCotizacionDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerCotizacionQuery(id), ct));

    /// <summary>
    /// Precarga como fletes por defecto el valor más alto propuesto por la Biblioteca
    /// en las líneas pendientes (lo usa la mesa del comercial).
    /// </summary>
    [HttpPost("{id:guid}/aplicar-propuestas")]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsAplicarPropuestasResultDto>> AplicarPropuestas(Guid id, CancellationToken ct = default)
        => Ok(await aplicarPropuestas.Handle(new clsAplicarPropuestasCommand(id), ct));

    /// <summary>Agregar el flete costeado de una línea.</summary>
    [HttpPost("{id:guid}/lineas")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsCotizacionDto>> AgregarLinea(Guid id, [FromBody] clsAgregarLineaCotizacionRequest req, CancellationToken ct = default)
        => Ok(await agregarLinea.Handle(new clsAgregarLineaCotizacionCommand(
            id, req.LineaSolicitudId, req.ValorFlete, req.ValorRetornoContenedor, req.Fuente), ct));

    /// <summary>Agregar servicios conexos a la cotización.</summary>
    [HttpPost("{id:guid}/conexos")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsCotizacionDto>> AgregarConexos(Guid id, [FromBody] clsAgregarConexosRequest req, CancellationToken ct = default)
        => Ok(await agregarConexos.Handle(new clsAgregarConexosCotizacionCommand(
            id, req.Items.Select(i => new clsConexoItem(i.LineaId, i.ServicioConexoId, i.Cantidad, i.ValorAplicado, i.Origen)).ToList()), ct));

    /// <summary>Corregir la cantidad (y opcionalmente el valor) de un conexo ya agregado —
    /// solo en cotización Borrador (S5-HU-18/#262).</summary>
    [HttpPatch("{id:guid}/conexos/{conexoId:guid}")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsCotizacionDto>> EditarConexo(Guid id, Guid conexoId, [FromBody] clsEditarConexoRequest req, CancellationToken ct = default)
        => Ok(await editarConexo.Handle(new clsEditarConexoCotizacionCommand(id, conexoId, req.Cantidad, req.ValorAplicado), ct));

    /// <summary>Quitar un conexo agregado por error — solo en cotización Borrador
    /// (S5-HU-18/#262).</summary>
    [HttpDelete("{id:guid}/conexos/{conexoId:guid}")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsCotizacionDto>> EliminarConexo(Guid id, Guid conexoId, CancellationToken ct = default)
        => Ok(await eliminarConexo.Handle(new clsEliminarConexoCotizacionCommand(id, conexoId), ct));

    /// <summary>
    /// Entregar la cotización a Comercial: marca la entrega y envía la cotización
    /// formal (PDF adjunto, si viene) por email al comercial responsable del cliente.
    /// 409 si ya se había entregado antes (no se reenvía dos veces).
    /// </summary>
    [HttpPost("{id:guid}/entregar")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsEntregarCotizacionResultDto>> Entregar(Guid id, IFormFile? formato = null, CancellationToken ct = default)
    {
        byte[]? pdf = null;
        if (formato is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await formato.CopyToAsync(ms, ct);
            pdf = ms.ToArray();
        }
        return Ok(await entregar.Handle(new clsEntregarCotizacionCommand(id, pdf, formato?.FileName), ct));
    }

    /// <summary>Registrar el resultado (ganada/perdida) y realimentar la Biblioteca. Solo
    /// Comercial y Admin (S4-HU-11.1/#213): ni AdminTarifas ni CoordinadorFletes deciden
    /// si una cotización se ganó o se perdió, eso es criterio del Comercial dueño del negocio.</summary>
    [HttpPost("{id:guid}/resultado")]
    [Authorize(Roles = "Comercial,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsCotizacionDto>> MarcarResultado(Guid id, [FromBody] clsMarcarResultadoRequest req, CancellationToken ct = default)
        => Ok(await marcarResultado.Handle(new clsMarcarResultadoCommand(id, req.Resultado, req.Motivo, req.Comentario), ct));

    /// <summary>Exporta a Excel el detalle completo de la cotización (fletes + servicios logísticos y adicionales).</summary>
    [HttpGet("{id:guid}/exportar-excel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportarExcel(Guid id, CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new clsExportarCotizacionExcelQuery(id), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
