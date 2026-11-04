using Nexo.Domain.PlanDeCargue.Entidades;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Tracked: quien lo pide va a mutar Estado.</summary>
public interface IPlanCargueRepository
{
    Task<clsPlanCargue?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Deja lista la fila espejo ANTES de publicar "crear_plan": solo se agrega al DbContext (el SaveChanges del handler flushea también el outbox).
    /// Asegura un stub de <c>clsVehiculoCubicacion</c> con ese Id solo para no romper la FK de <c>clsPlanCargue.VehiculoId</c>; nadie más lo lee.
    /// </summary>
    Task PrepararCreacionAsync(clsPlanCargue plan, CancellationToken ct);

    /// <summary>Borrado optimista de la fila espejo al publicar "eliminar_plan"; sin efecto si el plan no tiene fila en NEXO.</summary>
    Task EliminarSiExisteAsync(Guid id, CancellationToken ct);

    /// <summary>Listado paginado (1-based + pageSize) contra la proyección local, sin ítems/colocaciones.</summary>
    Task<(IReadOnlyList<clsPlanCargue> Items, int Total)> ListarRecientesAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>Los planes que comparten la MISMA LineaSolicitudId que <paramref name="planId"/>, cada uno con sus colocaciones (para contar cargadas/totales); solo el propio plan si no tiene LineaSolicitudId.</summary>
    Task<IReadOnlyList<clsPlanCargue>> ObtenerFlotaAsync(Guid planId, CancellationToken ct);

    /// <summary>Todos los planes que comparten <paramref name="lineaSolicitudId"/>; sirve cuando aún no existe un PlanId (sondeo de <see cref="clsCrearPlanDesdeLineaCommand"/>). Lista vacía si no ha llegado ninguno, nunca null.</summary>
    Task<IReadOnlyList<clsPlanCargue>> ObtenerPorLineaAsync(Guid lineaSolicitudId, CancellationToken ct);

    /// <summary>Si algún <see cref="clsPlanCargue"/> referencia el vehículo; se consulta ANTES de publicar "eliminar_vehiculo" porque la fila espejo sí vive en esta base.</summary>
    Task<bool> ExisteConVehiculoAsync(Guid vehiculoId, CancellationToken ct);
}
