using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.Trabajos;

/// <summary>
/// Canal de avisos del avance de los trabajos. Cada trabajo es un grupo de SignalR, así que un comercial solo recibe el avance de los pliegos que mira.
/// No reenvía lo perdido: quien estuvo desconectado lo recupera con GET /api/trabajos/{id}.
/// </summary>
[Authorize]
public sealed class clsTrabajosHub : Hub
{
    /// <summary>Nombre del grupo de un trabajo. Un solo sitio para que no se escriba de dos formas.</summary>
    public static string Grupo(Guid trabajoId) => $"trabajo:{trabajoId}";

    /// <summary>Empieza a recibir el avance de un trabajo.</summary>
    public Task Suscribir(Guid trabajoId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, Grupo(trabajoId));

    /// <summary>Deja de recibirlo. Al cerrar la pantalla, para no acumular grupos por conexión.</summary>
    public Task Desuscribir(Guid trabajoId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(trabajoId));
}
