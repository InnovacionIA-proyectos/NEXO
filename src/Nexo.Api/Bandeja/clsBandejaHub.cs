using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.Bandeja;

/// <summary>
/// Canal de avisos de la Bandeja Priorizada. Hub aparte de /hubs/trabajos y con un solo grupo: la bandeja es la misma para todo rol autenticado.
/// No reenvía lo perdido: quien estuvo desconectado consulta la bandeja al volver.
/// </summary>
// AdminBiblioteca no entra a la Bandeja ni a su canal de avisos.
[Authorize(Roles = "Comercial,AdminTarifas,ServiciosLogisticos,LiderProceso,Admin")]
public sealed class clsBandejaHub : Hub
{
    /// <summary>El único grupo: todos los conectados a la bandeja ven lo mismo.</summary>
    public const string Grupo = "bandeja";

    /// <summary>Empieza a recibir los avisos de cambio de la bandeja.</summary>
    public Task Suscribir() => Groups.AddToGroupAsync(Context.ConnectionId, Grupo);

    /// <summary>Deja de recibirlos. Al salir de la pantalla de la bandeja.</summary>
    public Task Desuscribir() => Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo);
}
