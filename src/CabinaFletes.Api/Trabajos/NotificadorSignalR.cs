using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CabinaFletes.Api.Trabajos;

/// <summary>
/// Implementa el puerto de avisos con SignalR.
///
/// Vive en Api porque SignalR es ASP.NET Core, y ni Application ni Infrastructure
/// pueden verlo. El contenedor las une en el arranque: el consumidor de Infrastructure
/// pide el puerto y recibe esto, sin saber qué hay detrás.
///
/// El nombre del evento, "avance", es parte del contrato con el frontend: es lo que el
/// cliente escucha.
/// </summary>
public sealed class NotificadorSignalR(IHubContext<TrabajosHub> hub) : INotificadorDeTrabajos
{
    public const string EventoAvance = "avance";

    public Task AvisarAsync(AvisoDeTrabajo aviso, CancellationToken ct = default) =>
        hub.Clients.Group(TrabajosHub.Grupo(aviso.TrabajoId)).SendAsync(EventoAvance, aviso, ct);
}
