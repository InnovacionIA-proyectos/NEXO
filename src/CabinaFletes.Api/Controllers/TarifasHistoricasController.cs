using CabinaFletes.Application.CasosDeUso.TarifasGanadas;
using CabinaFletes.Application.CasosDeUso.TarifasGanadas.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CabinaFletes.Api.Controllers;

/// <summary>Biblioteca de Tarifas Ganadas: histórico local (propio) reutilizable en el costeo.</summary>
[ApiController]
[Authorize]
[Route("api/tarifas-historicas")]
public class TarifasHistoricasController(
    IQueryHandler<SugerirTarifaHistoricaQuery, IReadOnlyList<BibliotecaItemDto>> sugerir,
    IQueryHandler<BuscarBibliotecaTarifasQuery, IReadOnlyList<BibliotecaItemDto>> buscar)
    : ControllerBase
{
    /// <summary>
    /// Con ruta/vehículo → sugerencias para el costeo. Con q/mercancia/vigencia/origen →
    /// búsqueda filtrada de la Biblioteca. Sin nada → las más recientes.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BibliotecaItemDto>>> Get(
        [FromQuery] string? q,
        [FromQuery] Guid? ruta,
        [FromQuery] Guid? vehiculo,
        [FromQuery] string? mercancia,
        [FromQuery] string? vigencia,
        [FromQuery] string? origen,      // "ganada" | "legado"
        CancellationToken ct = default)
    {
        if (ruta != null || vehiculo != null)
            return Ok(await sugerir.Handle(new SugerirTarifaHistoricaQuery(ruta, vehiculo, mercancia), ct));

        var hayFiltros = !string.IsNullOrWhiteSpace(q) || !string.IsNullOrWhiteSpace(mercancia)
            || !string.IsNullOrWhiteSpace(vigencia) || !string.IsNullOrWhiteSpace(origen);
        if (!hayFiltros)
            return Ok(await sugerir.Handle(new SugerirTarifaHistoricaQuery(null, null, null), ct));

        bool? deGanada = origen?.Trim().ToLowerInvariant() switch
        {
            "ganada" => true,
            "legado" => false,
            _ => null,
        };
        return Ok(await buscar.Handle(new BuscarBibliotecaTarifasQuery(q, mercancia, vigencia, deGanada), ct));
    }
}
