using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.PlanDeCargue;

/// <summary>Implementa el puerto de avisos de plan de cargue con SignalR. El nombre del
/// evento, "avance", es parte del contrato con el frontend.</summary>
public sealed class NotificadorPlanCargueSignalR(IHubContext<PlanCargueHub> hub) : INotificadorDePlanCargue
{
    public const string EventoAvance = "avance";

    public Task AvisarAsync(AvisoPlanCargueDto aviso, CancellationToken ct = default) =>
        hub.Clients.Group(PlanCargueHub.Grupo(aviso.PlanCargueId)).SendAsync(EventoAvance, aviso, ct);
}
