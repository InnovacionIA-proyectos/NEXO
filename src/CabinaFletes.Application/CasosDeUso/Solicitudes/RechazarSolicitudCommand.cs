using System.Text.Json;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Mensajes.Bandeja;
using CabinaFletes.Domain.Solicitudes.Entidades;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes;

/// <summary>Rechazo por el equipo comercial: descarta la solicitud y registra el motivo.</summary>
public sealed record RechazarSolicitudCommand(Guid SolicitudId, string Motivo, string? Comentario) : ICommand<Unit>;

public sealed class RechazarSolicitudCommandHandler(
    ISolicitudesRepository solicitudes, IAuditoriaPort auditoria, ICurrentUserPort usuarioActual,
    TimeProvider clock, IPublicadorDeMensajes publicador, IUnitOfWork uow)
    : ICommandHandler<RechazarSolicitudCommand, Unit>
{
    public async Task<Unit> Handle(RechazarSolicitudCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new NotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");
        if (s.Estado == EstadoSolicitud.Descartada)
            throw new ConflictException("SOLICITUD_YA_DESCARTADA", "La solicitud ya fue rechazada/descartada.");

        var condiciones = CondicionesServicioDto.Desde(s.CondicionesServicio) with
        {
            Rechazo = new MotivoRechazoDto(
                command.Motivo.Trim(),
                string.IsNullOrWhiteSpace(command.Comentario) ? null : command.Comentario.Trim(),
                clock.GetUtcNow()),
        };
        s.CondicionesServicio = JsonSerializer.Serialize(condiciones, CondicionesServicioDto.Json);
        s.Estado = EstadoSolicitud.Descartada;

        auditoria.Registrar(usuarioActual.Id, "solicitudes", s.Id, "rechazar",
            JsonSerializer.Serialize(new { motivo = command.Motivo.Trim(), comentario = command.Comentario }, CondicionesServicioDto.Json));
        // La solicitud sale de la cola de trabajo. Va por el Outbox, en la misma
        // transacción que el rechazo: no puede avisarse un rechazo que no se guardó.
        await publicador.PublicarAsync(
            new BandejaCambio(MotivoDeCambio.SolicitudRechazada, clock.GetUtcNow()), ct);

        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
