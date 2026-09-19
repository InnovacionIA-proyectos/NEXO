using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.DTOs;

namespace CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin.Interfaces;

/// <summary>
/// Caché local del piso regulatorio SICE-TAC (import periódico desde el Excel exportado
/// de la fuente oficial). Distinto de Biblioteca/ITarifarioSapPort, que consulta la
/// matriz EN VIVO desde el Data Warehouse: esta es la copia local que alimenta la Mesa
/// de Costeo cuando el DW no aplica o para consultas rápidas ya enlazadas a rutas.
/// </summary>
public interface ITarifasSicetacRepository
{
    Task<IReadOnlyList<TarifaSicetacDto>> ConsultarAsync(SicetacFiltro filtro, CancellationToken ct);

    /// <summary>
    /// Import todo-en-uno: parseo del Excel (columnas por encabezado), enlace a rutas
    /// locales por NOMBRERUTA, deduplicación (BD + dentro del mismo archivo) y upsert.
    /// Vive completo en Infrastructure por la misma razón pragmática que Biblioteca y
    /// ServiciosConexos: lectura de celdas ClosedXML entrelazada con reglas de enlace,
    /// sin separación que aporte testabilidad real.
    /// </summary>
    Task<ImportResumenDto> ImportarExcelAsync(Stream excel, Guid usuarioId, CancellationToken ct);

    Task<IReadOnlyList<ImportacionDto>> ListarImportacionesAsync(CancellationToken ct);
}
