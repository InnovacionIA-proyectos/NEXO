using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Trabajos.Repositorios;

/// <summary>Lee el estado del trabajo de la tabla de la saga; único punto que sabe que "un trabajo" es una saga de MassTransit.</summary>
public sealed class clsEfEstadoDeTrabajoRepository(clsNexoDbContext db, IPendientesDeCosteoPort pendientes) : IEstadoDeTrabajoPort
{
    public async Task<clsEstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default)
    {
        var saga = await db.Set<clsEstadoAutoCosteo>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == trabajoId, ct);

        if (saga is null) return null;

        // "Initial" es el estado interno de arranque de MassTransit; el cliente ve "Pendiente".
        var estado = saga.CurrentState == clsEfRegistroDeTrabajos.EstadoInicial ? "Pendiente" : saga.CurrentState;

        // Desglose: qué línea, qué pasó y si se resuelve sola o necesita a alguien.
        var pendientesDelTrabajo = await pendientes.DeTrabajoAsync(trabajoId, ct);

        return new clsEstadoTrabajoDto(
            saga.CorrelationId,
            estado,
            new clsAvanceTrabajoDto(
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
