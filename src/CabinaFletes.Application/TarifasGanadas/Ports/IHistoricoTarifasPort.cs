using CabinaFletes.Application.TarifasGanadas.Dtos;

namespace CabinaFletes.Application.TarifasGanadas.Ports;

/// <summary>
/// Biblioteca de Tarifas Ganadas: tarifas históricas propias (locales, ya cobradas en
/// cotizaciones ganadas o cargadas manualmente), NO la matriz SICE-TAC del DW (esa es
/// Biblioteca/ITarifarioSapPort). Vigencia() se calcula sobre TimeProvider.GetUtcNow().
/// </summary>
public interface IHistoricoTarifasPort
{
    /// <summary>Las 10 más recientes que calzan con ruta/vehículo/mercancía (sugerencia de costeo).</summary>
    Task<IReadOnlyList<BibliotecaItemDto>> SugerirAsync(Guid? rutaId, Guid? vehiculoId, string? mercancia, CancellationToken ct);

    /// <summary>Búsqueda filtrada de la Biblioteca (hasta 50 resultados).</summary>
    Task<IReadOnlyList<BibliotecaItemDto>> BuscarAsync(string? q, string? mercancia, string? vigencia, bool? deGanada, CancellationToken ct);
}
