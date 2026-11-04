using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfLineasDePliegoRepository(clsNexoDbContext db) : ILineasDePliegoPort
{
    /// <summary>Solo los identificadores, sin seguimiento: evita cargar cientos de entidades en memoria.</summary>
    public async Task<IReadOnlyList<Guid>> IdsPorSolicitudAsync(Guid solicitudId, CancellationToken ct = default) =>
        await db.LineasSolicitud.AsNoTracking()
            .Where(l => l.SolicitudId == solicitudId)
            .OrderBy(l => l.Id)
            .Select(l => l.Id)
            .ToListAsync(ct);
}
