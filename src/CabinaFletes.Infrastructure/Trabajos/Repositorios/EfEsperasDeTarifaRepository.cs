using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Trabajos.Repositorios;

public sealed class EfEsperasDeTarifaRepository(CabinaFletesDbContext db) : IEsperasDeTarifaPort
{
    public async Task RegistrarAsync(Guid trabajoId, Guid solicitudId, Guid lineaId, string motivo, CancellationToken ct = default)
    {
        // Idempotente: un reintento del mensaje no es una espera nueva. Sin esto, una
        // línea aparecería repetida en la pantalla tantas veces como se reentregara.
        var yaEsperaba = await db.Set<LineaEsperandoTarifa>()
            .AnyAsync(e => e.TrabajoId == trabajoId && e.LineaSolicitudId == lineaId && e.ResueltaEn == null, ct);
        if (yaEsperaba) return;

        db.Set<LineaEsperandoTarifa>().Add(new LineaEsperandoTarifa
        {
            TrabajoId = trabajoId,
            SolicitudId = solicitudId,
            LineaSolicitudId = lineaId,
            Motivo = motivo,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EsperaDeTarifaDto>> PendientesDeTrabajoAsync(Guid trabajoId, CancellationToken ct = default) =>
        await db.Set<LineaEsperandoTarifa>().AsNoTracking()
            .Where(e => e.TrabajoId == trabajoId && e.ResueltaEn == null)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EsperaDeTarifaDto(e.LineaSolicitudId, e.Motivo, e.CreatedAt))
            .ToListAsync(ct);
}
