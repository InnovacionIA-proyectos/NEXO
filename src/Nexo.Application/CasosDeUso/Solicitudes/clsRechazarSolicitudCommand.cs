using System.Text.Json;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Bandeja;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Rechazo por el equipo comercial: descarta la solicitud y registra el motivo.</summary>
public sealed record clsRechazarSolicitudCommand(Guid SolicitudId, string Motivo, string? Comentario) : ICommand<Unit>;

public sealed class clsRechazarSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual,
    TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow)
    : ICommandHandler<clsRechazarSolicitudCommand, Unit>
{
    public async Task<Unit> Handle(clsRechazarSolicitudCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");
        if (s.Estado == EstadoSolicitud.Descartada)
            throw new clsConflictException("SOLICITUD_YA_DESCARTADA", "La solicitud ya fue rechazada/descartada.");

        var condiciones = clsCondicionesServicioDto.Desde(s.CondicionesServicio) with
        {
            Rechazo = new clsMotivoRechazoDto(
                command.Motivo.Trim(),
                string.IsNullOrWhiteSpace(command.Comentario) ? null : command.Comentario.Trim(),
                clock.GetUtcNow()),
        };
        s.CondicionesServicio = JsonSerializer.Serialize(condiciones, clsCondicionesServicioDto.Json);
        s.Estado = EstadoSolicitud.Descartada;

        auditoria.Registrar(usuarioActual.Id, "solicitudes", s.Id, "rechazar",
            JsonSerializer.Serialize(new { motivo = command.Motivo.Trim(), comentario = command.Comentario }, clsCondicionesServicioDto.Json));
        // La solicitud sale de la cola de trabajo. Va por el Outbox, en la misma
        // transacción que el rechazo: no puede avisarse un rechazo que no se guardó.
        await publicador.PublicarAsync(
            new clsBandejaCambio(clsMotivoDeCambio.SolicitudRechazada, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
