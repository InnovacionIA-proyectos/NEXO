using Nexo.Application.CasosDeUso.Cubicacion;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Nexo.Api.Cubicacion;

public record clsMercanciaSolicitadaRequest(
    string CodigoMercancia, int Cantidad, decimal LargoCm, decimal AnchoCm, decimal AltoCm, decimal PesoKg)
{
    public clsMercanciaSolicitadaInput ADto() => new(CodigoMercancia, Cantidad, LargoCm, AnchoCm, AltoCm, PesoKg);
}

/// <summary>
/// "Mercancias" es obligatorio (400 MERCANCIAS_REQUERIDAS) pero se tipa con "?" a propósito: con Nullable Reference Types, [ApiController] trata un body no-nullable como [Required]
/// y responde su propio 400 VALIDATION_ERROR genérico antes de llegar a la validación explícita.
/// </summary>
public record clsCrearRecomendacionRequest(
    string IdLineaSolicitud, IReadOnlyList<clsMercanciaSolicitadaRequest>? Mercancias, string? ClaseViajeSeleccionada)
{
    public clsCrearRecomendacionInput ASolicitud(string? usuarioId) =>
        new(IdLineaSolicitud, Mercancias!.Select(m => m.ADto()).ToList(), ClaseViajeSeleccionada, usuarioId);
}

/// <summary>Recomendación de flota del Cubicador para Captura y Mesa de Costeo.</summary>
[ApiController]
[Route("api/cubicacion")]
public class clsCubicacionController(
    ICommandHandler<clsCrearRecomendacionCommand, clsRecomendacionDto> crear,
    IQueryHandler<clsConsultarRecomendacionQuery, clsRecomendacionDto> consultar,
    IQueryHandler<clsObtenerExplicacionRecomendacionQuery, clsExplicacionRecomendacionDto> explicacion,
    ICurrentUserPort usuarioActual) : ControllerBase
{
    /// <summary>
    /// Pide al Cubicador una recomendación de flota para una línea. <c>SIN_SELECCION_AUTOMATICA</c> y <c>condicionada</c> son resultados normales (200), no errores.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminBiblioteca,AdminTarifas,Admin")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsRecomendacionDto>> Crear([FromBody] clsCrearRecomendacionRequest body, CancellationToken ct = default)
    {
        if (body.Mercancias is null || body.Mercancias.Count == 0)
            throw new clsValidationFailedException("MERCANCIAS_REQUERIDAS", "La solicitud no trae ninguna mercancía.");

        var solicitud = body.ASolicitud(usuarioActual.Id.ToString());
        return Ok(await crear.Handle(new clsCrearRecomendacionCommand(solicitud), ct));
    }

    /// <summary>
    /// Reconsulta la última recomendación de una línea. <paramref name="origenSistema"/> es obligatorio: un mismo código de línea puede existir en NEXO y en Cotifly, y no se infiere del llamante.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminBiblioteca,AdminTarifas,Admin")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsRecomendacionDto>> Consultar(
        [FromQuery] string? idLineaSolicitud, [FromQuery] string? origenSistema, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idLineaSolicitud) || string.IsNullOrWhiteSpace(origenSistema))
            throw new clsValidationFailedException(
                "PARAMETROS_REQUERIDOS", "idLineaSolicitud y origenSistema son obligatorios.");

        return Ok(await consultar.Handle(new clsConsultarRecomendacionQuery(idLineaSolicitud, origenSistema), ct));
    }

    /// <summary>
    /// Por qué el Cubicador eligió esa flota, en lenguaje natural; se pide después de pintar los números (el modelo tarda). Mismos parámetros, validación y roles que <see cref="Consultar"/>.
    /// Siempre 200 con <c>{ "explicacion": texto | null }</c>: es opcional, así que cualquier fallo del Cubicador da <c>null</c>.
    /// </summary>
    [Authorize(Roles = "Comercial,AdminBiblioteca,AdminTarifas,Admin")]
    [HttpGet("explicacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsExplicacionRecomendacionDto>> Explicacion(
        [FromQuery] string? idLineaSolicitud, [FromQuery] string? origenSistema, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idLineaSolicitud) || string.IsNullOrWhiteSpace(origenSistema))
            throw new clsValidationFailedException(
                "PARAMETROS_REQUERIDOS", "idLineaSolicitud y origenSistema son obligatorios.");

        return Ok(await explicacion.Handle(new clsObtenerExplicacionRecomendacionQuery(idLineaSolicitud, origenSistema), ct));
    }
}
