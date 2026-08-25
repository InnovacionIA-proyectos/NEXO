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
public sealed class EfEstadoDeTrabajoRepository(CabinaFletesDbContext db, IEsperasDeTarifaPort esperas) : IEstadoDeTrabajoPort
{
    public async Task<EstadoTrabajoDto?> ObtenerAsync(Guid trabajoId, CancellationToken ct = default)
    {
        var saga = await db.Set<EstadoAutoCosteo>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.CorrelationId == trabajoId, ct);

        if (saga is null) return null;

        // "Initial" es el nombre interno del estado de arranque de MassTransit. El
        // cliente ve "Pendiente", que es lo que el contrato promete y lo que significa.
        var estado = saga.CurrentState == EfRegistroDeTrabajos.EstadoInicial ? "Pendiente" : saga.CurrentState;

        // El desglose de las que esperan a una persona: qué línea y qué le falta. Es la
        // diferencia entre "3 pendientes" y algo con lo que alguien puede actuar.
        var esperando = await esperas.PendientesDeTrabajoAsync(trabajoId, ct);

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
            esperando);
    }
}
