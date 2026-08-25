using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Trabajos.Repositorios;

public sealed class EfPendientesDeCosteoRepository(CabinaFletesDbContext db) : IPendientesDeCosteoPort
{
    public async Task RegistrarAsync(Guid trabajoId, Guid solicitudId, Guid lineaId,
                                     TipoPendienteDeCosteo tipo, string motivo, CancellationToken ct = default)
    {
        // Idempotente: un reintento del mensaje no es un pendiente nuevo. Sin esto la
        // misma línea aparecería repetida tantas veces como se reentregara.
        var yaEstaba = await db.Set<LineaPendienteDeCosteo>()
            .AnyAsync(p => p.TrabajoId == trabajoId
                        && p.LineaSolicitudId == lineaId
                        && p.Tipo == tipo
                        && p.ResueltaEn == null, ct);
        if (yaEstaba) return;

        db.Set<LineaPendienteDeCosteo>().Add(new LineaPendienteDeCosteo
        {
            TrabajoId = trabajoId,
            SolicitudId = solicitudId,
            LineaSolicitudId = lineaId,
            Tipo = tipo,
            // La columna admite 500: un motivo más largo se recorta en vez de tumbar el
            // registro, porque perder el pendiente es peor que perder la cola del texto.
            Motivo = motivo.Length > 500 ? motivo[..500] : motivo,
        });
    }

    public async Task<IReadOnlyList<LineaPendienteRef>> ManualesAbiertasAsync(CancellationToken ct = default) =>
        await db.Set<LineaPendienteDeCosteo>().AsNoTracking()
            .Where(p => p.Tipo == TipoPendienteDeCosteo.Manual && p.ResueltaEn == null)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new LineaPendienteRef(p.TrabajoId, p.SolicitudId, p.LineaSolicitudId))
            .ToListAsync(ct);

    public async Task MarcarResueltoAsync(Guid trabajoId, Guid lineaId, TipoPendienteDeCosteo tipo, CancellationToken ct = default)
    {
        // Se marca en vez de borrar: queda el rastro de cuánto tardó, que para el tipo
        // Manual es cuánto tardó la intervención de una persona. Ese dato es el que
        // permite saber si la cola del Administrador de Tarifas es un cuello de botella.
        var abiertos = await db.Set<LineaPendienteDeCosteo>()
            .Where(p => p.TrabajoId == trabajoId && p.LineaSolicitudId == lineaId
                     && p.Tipo == tipo && p.ResueltaEn == null)
            .ToListAsync(ct);

        foreach (var p in abiertos) p.ResueltaEn = DateTimeOffset.UtcNow;
    }

    public async Task<IReadOnlyList<PendienteDeCosteoDto>> DeTrabajoAsync(Guid trabajoId, CancellationToken ct = default) =>
        await db.Set<LineaPendienteDeCosteo>().AsNoTracking()
            .Where(p => p.TrabajoId == trabajoId && p.ResueltaEn == null)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PendienteDeCosteoDto(p.LineaSolicitudId, p.Tipo, p.Motivo, p.CreatedAt))
            .ToListAsync(ct);
}
