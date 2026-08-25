using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Trabajos.Repositorios;

/// <summary>
/// Lee el estado del trabajo de la tabla de la saga.
///
/// Este es el único punto del sistema que sabe que "un trabajo" es una saga de
/// MassTransit. Application ve un DTO y nada más.
/// </summary>
public sealed class EfEstadoDeTrabajoRepository(CabinaFletesDbContext db) : IEstadoDeTrabajoPort
{
    public async Task<EstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default)
    {
        var saga = await db.Set<EstadoAutoCosteo>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == trabajoId, ct);

        if (saga is null) return null;

        return new EstadoTrabajoDto(
            saga.CorrelationId,
            saga.CurrentState,
            new AvanceTrabajoDto(
                saga.TotalLineas,
                saga.Resueltas,
                saga.PendientesTecnicas + saga.RequierenManual,
                saga.PendientesTecnicas,
                saga.RequierenManual),
            saga.SolicitadoEn,
            saga.TerminadoEn);
    }
}
