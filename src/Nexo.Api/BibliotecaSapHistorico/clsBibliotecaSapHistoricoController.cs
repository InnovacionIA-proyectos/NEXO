using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using Nexo.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.BibliotecaSapHistorico;

/// <summary>
/// Biblioteca — histórico de tarifas SAP TM leído en vivo (SOLO LECTURA) del
/// datacenter corporativo (log de cambios dbsolucionesTI.dbo.tblSAPTarifasLog).
/// </summary>
[ApiController]
[Authorize]
[Route("api/biblioteca-sap-historico")]
public class clsBibliotecaSapHistoricoController(
    IQueryHandler<clsBuscarSapHistoricoQuery, clsSapHistoricoResultDto> buscar,
    IQueryHandler<clsFiltrosSapHistoricoQuery, clsSapHistoricoFiltrosDto> filtros)
    : ControllerBase
{
    /// <summary>
    /// Consulta el histórico ordenado por vigencia descendente, con filtros por
    /// texto (código DIVIPOLA de origen/destino o detalles) y línea de negocio.
    ///
    /// S6-HU-29.2/#312: <c>page</c> (1-based, opcional, def. 1) agrega la paginación que faltaba (el histórico tiene ~26 mil filas y
    /// <c>top</c> solo dejaba ver las primeras); <c>top</c> pasa a ser el tamaño de página. <c>total</c> sigue siendo el total sin
    /// paginar y el orden es estable. Cambio aditivo: mismo shape de respuesta (<c>{ total, items }</c>); sin <c>page</c> responde la
    /// primera página, como siempre.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult<clsSapHistoricoResultDto>> Get(
        [FromQuery] string? q,
        [FromQuery] int? lineaNegocio,
        [FromQuery] string? tipoVehiculo,
        [FromQuery] string? tipoCarroseria,
        [FromQuery] int? claseViaje,
        [FromQuery, Range(1, 500)] int top = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        CancellationToken ct = default)
        => Ok(await buscar.Handle(new clsBuscarSapHistoricoQuery(q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top, page), ct));

    /// <summary>Valores disponibles para los filtros por columna (solo los usados en el histórico).</summary>
    [HttpGet("filtros")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<clsSapHistoricoFiltrosDto>> Filtros(CancellationToken ct = default)
        => Ok(await filtros.Handle(new clsFiltrosSapHistoricoQuery(), ct));
}
