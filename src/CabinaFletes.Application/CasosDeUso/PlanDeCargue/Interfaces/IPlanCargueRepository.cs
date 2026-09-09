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
}
