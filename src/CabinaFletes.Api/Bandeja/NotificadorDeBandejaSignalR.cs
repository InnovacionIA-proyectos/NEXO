using CabinaFletes.Application.CasosDeUso.Bandeja.DTOs;
using CabinaFletes.Application.CasosDeUso.Bandeja.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace CabinaFletes.Api.Bandeja;

/// <summary>
/// Implementa el puerto de avisos de bandeja con SignalR.
///
/// El nombre del evento, "bandeja", es parte del contrato con el frontend: es lo que el
/// cliente escucha. Se declara aquí en una constante para que no se escriba de dos formas
/// distintas en dos sitios.
/// </summary>
public sealed class NotificadorDeBandejaSignalR(IHubContext<BandejaHub> hub) : INotificadorDeBandeja
{
    public const string EventoBandeja = "bandeja";

    public Task AvisarAsync(AvisoDeBandeja aviso, CancellationToken ct = default) =>
        hub.Clients.Group(BandejaHub.Grupo).SendAsync(EventoBandeja, aviso, ct);
}
