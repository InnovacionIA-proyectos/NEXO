using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// Lectura del catálogo de vehículos proyectado en NEXO (clsVehiculoCatalogoConsumer
/// lo mantiene sincronizado). Sin escritura: los vehículos se crean/editan por el
/// comando "guardar_vehiculo", nunca directo contra esta tabla.
/// </summary>
public interface IVehiculoCubicacionRepository
{
    /// <summary>Pagina 1-based. Devuelve la página pedida + el total
    /// real de filas que cumplen (sin filtro alguno hoy: es el catálogo completo).</summary>
    Task<(IReadOnlyList<clsVehiculoCubicacion> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct);

    Task<clsVehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct);
}
