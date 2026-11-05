using System.ComponentModel.DataAnnotations;
using Nexo.Api.Common.RateLimiting;
using Nexo.Application.CasosDeUso.TarifasGanadas;
using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexo.Api.TarifasGanadas;

/// <summary>Biblioteca de Tarifas Ganadas: histórico local (propio) reutilizable en el costeo.</summary>
[ApiController]
[Authorize]
[Route("api/tarifas-historicas")]
public class clsTarifasHistoricasController(
    IQueryHandler<clsSugerirTarifaHistoricaQuery, IReadOnlyList<clsBibliotecaItemDto>> sugerir,
    IQueryHandler<clsBuscarBibliotecaTarifasQuery, clsPaginaDto<clsBibliotecaItemDto>> buscar)
    : ControllerBase
{
    /// <summary>
    /// Con ruta/vehículo: las 10 más recientes como sugerencia de costeo (sin paginar). Con q/mercancia/vigencia/origen: búsqueda filtrada y paginada (<c>{ items, total }</c>). Sin nada: las más recientes sin paginar.
    /// El shape depende de la rama (array plano vs. <c>{ items, total }</c>): son dos casos de uso que comparten ruta.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    public async Task<ActionResult> Get(
        [FromQuery] string? q,
        [FromQuery] Guid? ruta,
        [FromQuery] Guid? vehiculo,
        [FromQuery] string? mercancia,
        [FromQuery] string? vigencia,
        [FromQuery] string? origen,      // "ganada" | "legado"
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (ruta != null || vehiculo != null)
            return Ok(await sugerir.Handle(new clsSugerirTarifaHistoricaQuery(ruta, vehiculo, mercancia), ct));

        var hayFiltros = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(mercancia)
            || !string.IsNullOrWhiteSpace(vigencia) || !string.IsNullOrWhiteSpace(origen);
        if (!hayFiltros)
            return Ok(await sugerir.Handle(new clsSugerirTarifaHistoricaQuery(null, null, null), ct));

        bool? deGanada = origen?.Trim().ToLowerInvariant() switch
        {
            "ganada" => true,
            "legado" => false,
            _ => null,
        };
        return Ok(await buscar.Handle(new clsBuscarBibliotecaTarifasQuery(q, mercancia, vigencia, deGanada, page, pageSize), ct));
    }
}
