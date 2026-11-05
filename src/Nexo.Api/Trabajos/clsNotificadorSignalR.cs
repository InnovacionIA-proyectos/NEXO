using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Nexo.Api.Trabajos;

/// <summary>Implementa el puerto de avisos con SignalR (vive en Api porque Application e Infrastructure no ven SignalR). El nombre del evento, "avance", es parte del contrato con el frontend.</summary>
public sealed class clsNotificadorSignalR(IHubContext<clsTrabajosHub> hub) : INotificadorDeTrabajos
{
    public const string EventoAvance = "avance";

    public Task AvisarAsync(clsAvisoDeTrabajo aviso, CancellationToken ct = default) =>
        hub.Clients.Group(clsTrabajosHub.Grupo(aviso.TrabajoId)).SendAsync(EventoAvance, aviso, ct);
}
