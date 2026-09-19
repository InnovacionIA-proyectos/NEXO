using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.Bandeja;

/// <summary>
/// Canal de avisos de la Bandeja Priorizada.
///
/// HUB APARTE Y NO MÉTODOS EN EL DE TRABAJOS. La ruta /hubs/trabajos ya está publicada en
/// el contrato v1.3.0 como el canal del avance de UN trabajo, con un grupo por trabajo.
/// Meterle la bandeja dejaría el nombre mintiendo y obligaría a renombrar una ruta que el
/// frontend ya tiene. Este es aditivo: quien no use la bandeja no se entera.
///
/// UN SOLO GRUPO, al contrario que allá. La bandeja no está filtrada por usuario —
/// cualquier rol autenticado ve la misma— así que no hay nada que separar. Los grupos de
/// trabajos existen porque un comercial no tiene por qué recibir el avance de pliegos
/// ajenos; aquí ese problema no existe.
///
/// NO REENVÍA LO PERDIDO. Quien estuvo desconectado no recibe los avisos de mientras: al
/// volver, consulta la bandeja y ya está al día. Es el mismo reparto de siempre — el
/// aviso es la mejora, la consulta es la garantía.
/// </summary>
[Authorize]
public sealed class clsBandejaHub : Hub
{
    /// <summary>
    /// El único grupo. Es constante y no calculado, porque no hay nada que parametrizar:
    /// todos los conectados a la bandeja ven lo mismo.
    /// </summary>
    public const string Grupo = "bandeja";

    /// <summary>Empieza a recibir los avisos de cambio de la bandeja.</summary>
    public Task Suscribir() => Groups.AddToGroupAsync(Context.ConnectionId, Grupo);

    /// <summary>Deja de recibirlos. Al salir de la pantalla de la bandeja.</summary>
    public Task Desuscribir() => Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo);
}
