using CabinaFletes.Domain.PlanDeCargue.Entidades;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Tracked: quien lo pide va a mutar Estado (S2-HU-08.1).</summary>
public interface IPlanCargueRepository
{
    Task<PlanCargue?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// S3-HU-02 (issue #104): dado que <c>CrearPlanCommand</c> genera el PlanId en NEXO
    /// (no en Cubicador), este método deja lista la fila espejo ANTES de publicar
    /// "crear_plan" — solo se agrega al DbContext, no se guarda (el <c>SaveChangesAsync</c>
    /// posterior del handler es el mismo que flushea el outbox del mensaje, ver
    /// ICubicacionPort). También asegura un stub de <c>VehiculoCubicacion</c> con ese Id si
    /// no existe todavía: <c>PlanCargue.VehiculoId</c> tiene FK contra la tabla de
    /// vehículos DE ESTA base, pero la fuente de verdad de vehículos es Cubicador — el
    /// stub (solo Id + Nombre) existe únicamente para no romper esa FK, nadie más lo lee.
    /// </summary>
    Task PrepararCreacionAsync(PlanCargue plan, CancellationToken ct);

    /// <summary>
    /// S3-HU-02 (issue #104): borrado optimista de la fila espejo al publicar
    /// "eliminar_plan" — sin efecto si el plan no tiene fila en NEXO (p.ej. nunca se creó
    /// por acá). No hace falta lo contrario (reponerla si Cubicador rechaza el borrado):
    /// es una operación rara de deshacer y de bajo riesgo, ver EliminarPlanCommand.
    /// </summary>
    Task EliminarSiExisteAsync(Guid id, CancellationToken ct);

    /// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue — listado contra la
    /// proyección local, sin ítems/colocaciones (sería demasiado para un listado).</summary>
    Task<IReadOnlyList<PlanCargue>> ListarRecientesAsync(int top, CancellationToken ct);

    /// <summary>S3-HU-03 (issue #112): GET /api/planes-cargue/{id}/flota — los planes
    /// (vehículos) que comparten la MISMA LineaSolicitudId que <paramref name="planId"/>,
    /// cada uno con sus colocaciones (para contar cargadas/totales) — o solo el propio
    /// plan si no tiene LineaSolicitudId (flota de un solo vehículo).</summary>
    Task<IReadOnlyList<PlanCargue>> ObtenerFlotaAsync(Guid planId, CancellationToken ct);

    /// <summary>
    /// [S3-HU-04.3, issue #78] GET /api/planes-cargue/por-linea/{lineaId} — todos los
    /// planes que comparten <paramref name="lineaSolicitudId"/>, directo — a diferencia de
    /// <see cref="ObtenerFlotaAsync"/>, que parte de un PlanId ya conocido, este es lo que
    /// hace falta cuando ese PlanId TODAVÍA no existe (sondeo de
    /// <see cref="CrearPlanDesdeLineaCommand"/> mientras el comando sigue en proceso).
    /// Lista vacía si ningún plan de esa línea ha llegado todavía — nunca null.
    /// </summary>
    Task<IReadOnlyList<PlanCargue>> ObtenerPorLineaAsync(Guid lineaSolicitudId, CancellationToken ct);
}
