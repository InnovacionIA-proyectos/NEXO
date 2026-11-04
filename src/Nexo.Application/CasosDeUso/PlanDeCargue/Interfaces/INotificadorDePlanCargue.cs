using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;

namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// Empuja el desenlace de un plan de cargue a quien lo esté mirando. Va aparte de INotificadorDeTrabajos porque ese contrato lleva
/// Avance/pendientes/reintentos, sin sentido para un solo plan. El aviso no es garantía: la verdad vive en GET /api/planes-cargue/{id}.
/// </summary>
public interface INotificadorDePlanCargue
{
    Task AvisarAsync(clsAvisoPlanCargueDto aviso, CancellationToken ct = default);
}
