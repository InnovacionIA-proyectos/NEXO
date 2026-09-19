using Nexo.Application.CasosDeUso.TarifasGanadas.DTOs;

namespace Nexo.Application.CasosDeUso.TarifasGanadas.Interfaces;

/// <summary>
/// Biblioteca de Tarifas Ganadas: tarifas históricas propias (locales, ya cobradas en
/// cotizaciones ganadas o cargadas manualmente), NO la matriz SICE-TAC del DW (esa es
/// Biblioteca/ITarifarioSapPort). Vigencia() se calcula sobre TimeProvider.GetUtcNow().
/// </summary>
public interface IHistoricoTarifasPort
{
    /// <summary>Las 10 más recientes que calzan con ruta/vehículo/mercancía (sugerencia de costeo).</summary>
    Task<IReadOnlyList<BibliotecaItemDto>> SugerirAsync(Guid? rutaId, Guid? vehiculoId, string? mercancia, CancellationToken ct);

    /// <summary>Búsqueda filtrada de la Biblioteca. Issue #166: page 1-based +
    /// pageSize, reemplaza el tope fijo de 50 (que no exponía el total real ni
    /// dejaba pedir más) por paginación real.</summary>
    Task<(IReadOnlyList<BibliotecaItemDto> Items, int Total)> BuscarAsync(
        string? q, string? mercancia, string? vigencia, bool? deGanada, int page, int pageSize, CancellationToken ct);
}
