using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.PlanDeCargue;

/// <summary>
/// Canal de avisos del desenlace de un plan de cargue, aparte de /hubs/trabajos (aquí no hay desenlaces parciales).
/// No reenvía lo perdido: un cliente desconectado recupera el estado con GET /api/planes-cargue/{id}.
/// </summary>
[Authorize]
public sealed class clsPlanCargueHub : Hub
{
    public static string Grupo(Guid planCargueId) => $"plan-cargue:{planCargueId}";

    public Task Suscribir(Guid planCargueId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupo(planCargueId));

    public Task Desuscribir(Guid planCargueId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(planCargueId));
}
