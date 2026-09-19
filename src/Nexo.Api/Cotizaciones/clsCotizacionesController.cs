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
    public async Task<ActionResult<clsCotizacionDto>> Crear([FromBody] clsCrearCotizacionRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearCotizacionCommand(req.SolicitudId), ct);
        return Created($"/api/cotizaciones/{dto.Id}", dto);
    }

    /// <summary>Consolidado/total de la cotización integral.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<clsCotizacionDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerCotizacionQuery(id), ct));

    /// <summary>
    /// Precarga como fletes por defecto el valor más alto propuesto por la Biblioteca
    /// en las líneas pendientes (lo usa la mesa del comercial).
    /// </summary>
    [HttpPost("{id:guid}/aplicar-propuestas")]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<clsAplicarPropuestasResultDto>> AplicarPropuestas(Guid id, CancellationToken ct = default)
        => Ok(await aplicarPropuestas.Handle(new clsAplicarPropuestasCommand(id), ct));

    /// <summary>Agregar el flete costeado de una línea.</summary>
    [HttpPost("{id:guid}/lineas")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<clsCotizacionDto>> AgregarLinea(Guid id, [FromBody] clsAgregarLineaCotizacionRequest req, CancellationToken ct = default)
        => Ok(await agregarLinea.Handle(new clsAgregarLineaCotizacionCommand(
            id, req.LineaSolicitudId, req.ValorFlete, req.ValorRetornoContenedor, req.Fuente), ct));

    /// <summary>Agregar servicios conexos a la cotización.</summary>
    [HttpPost("{id:guid}/conexos")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    public async Task<ActionResult<clsCotizacionDto>> AgregarConexos(Guid id, [FromBody] clsAgregarConexosRequest req, CancellationToken ct = default)
        => Ok(await agregarConexos.Handle(new clsAgregarConexosCotizacionCommand(
            id, req.Items.Select(i => new clsConexoItem(i.ServicioConexoId, i.Cantidad, i.ValorAplicado)).ToList()), ct));

    /// <summary>
    /// Entregar la cotización a Comercial: marca la entrega y envía la cotización
    /// formal (PDF adjunto, si viene) por email al comercial responsable del cliente.
    /// </summary>
    [HttpPost("{id:guid}/entregar")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
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

    /// <summary>Registrar el resultado (ganada/perdida) y realimentar la Biblioteca.</summary>
    [HttpPost("{id:guid}/resultado")]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<clsCotizacionDto>> MarcarResultado(Guid id, [FromBody] clsMarcarResultadoRequest req, CancellationToken ct = default)
        => Ok(await marcarResultado.Handle(new clsMarcarResultadoCommand(id, req.Resultado, req.Motivo, req.Comentario), ct));

    /// <summary>Exporta a Excel el detalle completo de la cotización (fletes + servicios logísticos y adicionales).</summary>
    [HttpGet("{id:guid}/exportar-excel")]
    public async Task<IActionResult> ExportarExcel(Guid id, CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new clsExportarCotizacionExcelQuery(id), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
