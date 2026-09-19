using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// Empuja el desenlace de un plan de cargue a quien lo esté mirando (S2-HU-08.1).
///
/// Va aparte de INotificadorDeTrabajos a propósito: ese contrato lleva Avance con
/// pendientes/reintentos técnicos, conceptos del auto-costeo que no tienen sentido para
/// un plan de cargue (es un solo trabajo, no 300 líneas con desenlaces parciales).
/// Forzar la misma forma dejaría el nombre mintiendo — mismo criterio que ya separó
/// /hubs/bandeja de /hubs/trabajos.
///
/// EL AVISO NO ES GARANTÍA: la verdad vive en GET /api/planes-cargue/{id}.
/// </summary>
public interface INotificadorDePlanCargue
{
    Task AvisarAsync(AvisoPlanCargueDto aviso, CancellationToken ct = default);
}
