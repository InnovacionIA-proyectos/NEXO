using Nexo.Application.CasosDeUso.Biblioteca.DTOs;

namespace Nexo.Application.CasosDeUso.Biblioteca.Interfaces;

/// <summary>
/// Consulta de SOLO LECTURA a la matriz oficial SICE-TAC del Data Warehouse
/// (DWSice.dbo.tblRutasSicetac). Null/lista vacía cuando DwSice no está
/// configurado. Implementado por Infrastructure/Adapters/clsBibliotecaSicetacAdapter.cs.
/// </summary>
public interface IBibliotecaSicetacPort
{
    Task<clsMatrizSicetacResultDto> BuscarAsync(
        string? q, int? periodo, string? configuracion, string? tipoCarga, string? unidadTransporte,
        string? condicionCarga, string? viaEstandar, int top, int page = 1, CancellationToken ct = default);

    Task<IReadOnlyList<int>> PeriodosAsync(CancellationToken ct = default);

    Task<clsSicetacFiltrosDto> FiltrosAsync(CancellationToken ct = default);
}
