using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CabinaFletes.Api.Trabajos;

/// <summary>
/// Canal de avisos del avance de los trabajos.
///
/// Cada trabajo es un grupo de SignalR, así que un comercial solo recibe el avance de
/// los pliegos que está mirando y no el de los ajenos. Sin grupos, todo conectado
/// recibiría todo.
///
/// Va con [Authorize] como el resto del API: el canal no es una puerta trasera.
///
/// NO REENVÍA LO PERDIDO, y eso es importante que quede dicho. Un cliente que estuvo
/// desconectado no recibe lo que se perdió; lo recupera consultando
/// GET /api/trabajos/{id}. El aviso es una mejora, la consulta es la garantía.
/// </summary>
[Authorize]
public sealed class TrabajosHub : Hub
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
