using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.PlanDeCargue;

/// <summary>
/// Canal de avisos del desenlace de un plan de cargue.
///
/// Va aparte de /hubs/trabajos a propósito: ese canal ya está publicado en el contrato
/// como el avance de un pliego con líneas pendientes/reintentos técnicos — conceptos que
/// no aplican acá (un plan de cargue es un solo trabajo, optimizado o rechazado, sin
/// desenlaces parciales). Mismo criterio que ya separó /hubs/bandeja de /hubs/trabajos.
///
/// NO REENVÍA LO PERDIDO: un cliente desconectado recupera el estado con
/// GET /api/planes-cargue/{id}, no con este canal.
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
