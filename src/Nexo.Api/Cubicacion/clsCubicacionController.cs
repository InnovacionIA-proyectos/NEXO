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
/// "Mercancias" es obligatorio en negocio (400 MERCANCIAS_REQUERIDAS si falta o viene
/// vacío) pero se tipa como <c>IReadOnlyList&lt;...&gt;?</c> y no sin "?" a propósito: con
/// Nullable Reference Types habilitado, [ApiController] trata un parámetro de body
/// no-nullable como implícitamente [Required] y responde su propio 400 VALIDATION_ERROR
/// genérico (en inglés, sin pasar por el método) antes de llegar a la validación explícita
/// de abajo — mismo problema ya documentado y revertido en
/// <see cref="Api.Solicitudes.clsCapturaSolicitudesController.Aprovisionamiento"/>.
/// </summary>
public record clsCrearRecomendacionRequest(
    string IdLineaSolicitud, IReadOnlyList<clsMercanciaSolicitadaRequest>? Mercancias, string? ClaseViajeSeleccionada)
{
    public clsCrearRecomendacionInput ASolicitud(string? usuarioId) =>
        new(IdLineaSolicitud, Mercancias!.Select(m => m.ADto()).ToList(), ClaseViajeSeleccionada, usuarioId);
}

/// <summary>
/// S5-HU-18.1/#273: reemplaza por completo al contrato viejo de <c>cubicar()</c>
/// (<c>captura-api.service.ts</c>, usado por <c>captura-page</c> y <c>mesa-costeo-page</c>).
/// Mismos roles que el resto de Cubicación porque las dos pantallas que lo usan pertenecen
/// a Comercial (captura) y a Coordinación de Fletes (mesa de costeo).
/// </summary>
[ApiController]
[Route("api/cubicacion")]
public class clsCubicacionController(
    ICommandHandler<clsCrearRecomendacionCommand, clsRecomendacionDto> crear,
    IQueryHandler<clsConsultarRecomendacionQuery, clsRecomendacionDto> consultar,
    IQueryHandler<clsObtenerExplicacionRecomendacionQuery, clsExplicacionRecomendacionDto> explicacion,
    ICurrentUserPort usuarioActual) : ControllerBase
{
    /// <summary>
    /// Pide al Cubicador una recomendación de flota para una línea — reemplaza la
    /// cubicación rápida sin persistencia de antes. <c>SIN_SELECCION_AUTOMATICA</c> y
    /// <c>condicionada</c> son resultados normales de esta llamada, NO errores: siguen
    /// devolviendo 200, con el detalle dentro de <c>resultado</c>/<c>motivoSinSeleccion</c>.
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
    /// Reconsulta la última recomendación de una línea. <paramref name="origenSistema"/> es
    /// obligatorio: un mismo código de línea puede existir tanto en NEXO como en Cotifly,
    /// como rutas distintas, y no se infiere del llamante.
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
    /// Por qué el Cubicador eligió esa flota, en lenguaje natural. Se pide DESPUÉS de pintar los números de la recomendación: el modelo tarda
    /// varios segundos. Mismos parámetros, validación y roles que <see cref="Consultar"/>. <b>Siempre 200</b> con <c>{ "explicacion": texto | null }</c>:
    /// es un texto opcional, así que un Cubicador caído, sin configurar, sin responder, sin recomendación para la línea o sin explicación
    /// también dan <c>null</c> y la pantalla simplemente no muestra el bloque. Por aquí no pasa ninguna tarifa ni valor comercial.
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
