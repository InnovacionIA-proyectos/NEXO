using CabinaFletes.Application.Biblioteca.Dtos;

namespace CabinaFletes.Application.Biblioteca.Ports;

/// <summary>
/// Consulta de SOLO LECTURA a la matriz oficial SICE-TAC del Data Warehouse
/// (DWSice.dbo.tblRutasSicetac). Null/lista vacía cuando DwSice no está
/// configurado. Implementado por Infrastructure/Adapters/BibliotecaSicetacAdapter.cs.
/// </summary>
public interface IBibliotecaSicetacPort
{
    Task<MatrizSicetacResultDto> BuscarAsync(
        string? q, int? periodo, string? configuracion, string? tipoCarga, string? unidadTransporte,
        string? condicionCarga, string? viaEstandar, int top, CancellationToken ct = default);

    Task<IReadOnlyList<int>> PeriodosAsync(CancellationToken ct = default);

    Task<SicetacFiltrosDto> FiltrosAsync(CancellationToken ct = default);
}
