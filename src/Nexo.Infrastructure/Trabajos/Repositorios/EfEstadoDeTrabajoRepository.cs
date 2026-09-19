using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Trabajos.Repositorios;

/// <summary>
/// Lee el estado del trabajo de la tabla de la saga.
///
/// Este es el único punto del sistema que sabe que "un trabajo" es una saga de
/// MassTransit. Application ve un DTO y nada más.
/// </summary>
public sealed class EfEstadoDeTrabajoRepository(NexoDbContext db, IPendientesDeCosteoPort pendientes) : IEstadoDeTrabajoPort
{
    public async Task<EstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default)
    {
        var saga = await db.Set<EstadoAutoCosteo>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == trabajoId, ct);

        if (saga is null) return null;

        // "Initial" es el nombre interno del estado de arranque de MassTransit. El
        // cliente ve "Pendiente", que es lo que el contrato promete y lo que significa.
        var estado = saga.CurrentState == EfRegistroDeTrabajos.EstadoInicial ? "Pendiente" : saga.CurrentState;

        // El desglose: qué línea, qué pasó y si se resuelve sola o necesita a alguien.
        // Es la diferencia entre "3 pendientes" y algo con lo que se puede actuar.
        var pendientesDelTrabajo = await pendientes.DeTrabajoAsync(trabajoId, ct);

        return new EstadoTrabajoDto(
            saga.CorrelationId,
            estado,
            new AvanceTrabajoDto(
                saga.TotalLineas,
                saga.Resueltas,
                saga.PendientesTecnicas + saga.RequierenManual,
                saga.PendientesTecnicas,
                saga.RequierenManual),
            saga.SolicitadoEn,
            saga.TerminadoEn,
            pendientesDelTrabajo,
            saga.Secuencia);
    }
}
