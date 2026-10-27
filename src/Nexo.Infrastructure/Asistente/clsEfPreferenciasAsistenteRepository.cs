using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Domain.Asistente.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Asistente;

public sealed class clsEfPreferenciasAsistenteRepository(clsNexoDbContext db, TimeProvider reloj) : IPreferenciasAsistenteRepository
{
    public async Task<IReadOnlyList<clsPreferenciaAsistente>> ListarAsync(Guid usuarioId, CancellationToken ct = default) =>
        await db.PreferenciasAsistente.AsNoTracking().Where(p => p.UsuarioId == usuarioId).OrderBy(p => p.CreatedAt).ToListAsync(ct);

    public async Task<clsPreferenciaAsistente> CrearAsync(clsPreferenciaAsistente preferencia, CancellationToken ct = default)
    {
        preferencia.CreatedAt = reloj.GetUtcNow();
        db.PreferenciasAsistente.Add(preferencia);
        await db.SaveChangesAsync(ct);
        return preferencia;
    }

    public async Task<bool> EliminarAsync(Guid id, Guid usuarioId, CancellationToken ct = default) =>
        await db.PreferenciasAsistente.Where(p => p.Id == id && p.UsuarioId == usuarioId).ExecuteDeleteAsync(ct) == 1;
}
