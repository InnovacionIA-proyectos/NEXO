using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Lectura del catálogo de vehículos proyectado en NEXO; sin escritura (se crean/editan por el comando "guardar_vehiculo").</summary>
public interface IVehiculoCubicacionRepository
{
    /// <summary>Pagina 1-based. Devuelve la página pedida y el total real de filas (hoy el catálogo completo).</summary>
    Task<(IReadOnlyList<clsVehiculoCubicacion> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct);

    Task<clsVehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct);
}
