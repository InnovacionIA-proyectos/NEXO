using CabinaFletes.Api.Dtos;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Cotizaciones.Commands;
using CabinaFletes.Application.Cotizaciones.Dtos;
using CabinaFletes.Application.Cotizaciones.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

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
public class CotizacionesController(
    ICommandHandler<CrearCotizacionCommand, CotizacionDto> crear,
    ICommandHandler<AgregarLineaCotizacionCommand, CotizacionDto> agregarLinea,
    ICommandHandler<AgregarConexosCotizacionCommand, CotizacionDto> agregarConexos,
    ICommandHandler<AplicarPropuestasCommand, AplicarPropuestasResultDto> aplicarPropuestas,
    ICommandHandler<EntregarCotizacionCommand, EntregarCotizacionResultDto> entregar,
    ICommandHandler<MarcarResultadoCommand, CotizacionDto> marcarResultado,
    IQueryHandler<ObtenerCotizacionQuery, CotizacionDto> obtener,
    IQueryHandler<ExportarCotizacionExcelQuery, ArchivoDto> exportarExcel)
    : ControllerBase
{
    /// <summary>Crear o recuperar la cotización (borrador) de una solicitud.</summary>
    [HttpPost]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<CotizacionDto>> Crear([FromBody] CrearCotizacionRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new CrearCotizacionCommand(req.SolicitudId), ct);
        return Created($"/api/cotizaciones/{dto.Id}", dto);
    }

    /// <summary>Consolidado/total de la cotización integral.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CotizacionDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await obtener.Handle(new ObtenerCotizacionQuery(id), ct));

    /// <summary>
    /// Precarga como fletes por defecto el valor más alto propuesto por la Biblioteca
    /// en las líneas pendientes (lo usa la mesa del comercial).
    /// </summary>
    [HttpPost("{id:guid}/aplicar-propuestas")]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<AplicarPropuestasResultDto>> AplicarPropuestas(Guid id, CancellationToken ct = default)
        => Ok(await aplicarPropuestas.Handle(new AplicarPropuestasCommand(id), ct));

    /// <summary>Agregar el flete costeado de una línea.</summary>
    [HttpPost("{id:guid}/lineas")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<CotizacionDto>> AgregarLinea(Guid id, [FromBody] AgregarLineaCotizacionRequest req, CancellationToken ct = default)
        => Ok(await agregarLinea.Handle(new AgregarLineaCotizacionCommand(
            id, req.LineaSolicitudId, req.ValorFlete, req.ValorRetornoContenedor, req.Fuente), ct));

    /// <summary>Agregar servicios conexos a la cotización.</summary>
    [HttpPost("{id:guid}/conexos")]
    [Authorize(Roles = "Comercial,CoordinadorFletes,Admin")]
    public async Task<ActionResult<CotizacionDto>> AgregarConexos(Guid id, [FromBody] AgregarConexosRequest req, CancellationToken ct = default)
        => Ok(await agregarConexos.Handle(new AgregarConexosCotizacionCommand(
            id, req.Items.Select(i => new ConexoItem(i.ServicioConexoId, i.Cantidad, i.ValorAplicado)).ToList()), ct));

    /// <summary>
    /// Entregar la cotización a Comercial: marca la entrega y envía la cotización
    /// formal (PDF adjunto, si viene) por email al comercial responsable del cliente.
    /// </summary>
    [HttpPost("{id:guid}/entregar")]
    [Authorize(Roles = "AdminTarifas,CoordinadorFletes,Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EntregarCotizacionResultDto>> Entregar(Guid id, IFormFile? formato = null, CancellationToken ct = default)
    {
        byte[]? pdf = null;
        if (formato is { Length: > 0 })
        {
            await using var ms = new MemoryStream();
            await formato.CopyToAsync(ms, ct);
            pdf = ms.ToArray();
        }
        return Ok(await entregar.Handle(new EntregarCotizacionCommand(id, pdf, formato?.FileName), ct));
    }

    /// <summary>Registrar el resultado (ganada/perdida) y realimentar la Biblioteca.</summary>
    [HttpPost("{id:guid}/resultado")]
    [Authorize(Roles = "Comercial,AdminTarifas,CoordinadorFletes,Admin")]
    public async Task<ActionResult<CotizacionDto>> MarcarResultado(Guid id, [FromBody] MarcarResultadoRequest req, CancellationToken ct = default)
        => Ok(await marcarResultado.Handle(new MarcarResultadoCommand(id, req.Resultado, req.Motivo, req.Comentario), ct));

    /// <summary>Exporta a Excel el detalle completo de la cotización (fletes + servicios logísticos y adicionales).</summary>
    [HttpGet("{id:guid}/exportar-excel")]
    public async Task<IActionResult> ExportarExcel(Guid id, CancellationToken ct = default)
    {
        var archivo = await exportarExcel.Handle(new ExportarCotizacionExcelQuery(id), ct);
        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
