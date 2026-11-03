using Nexo.Application.CasosDeUso.RangosPeso.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.RangosPeso.Repositorios;

public sealed class clsEfRangosPesoRepository(clsNexoDbContext db) : IRangosPesoRepository
{
    public async Task<IReadOnlyList<clsRangoPeso>> ListarAsync(CancellationToken ct) =>
        await db.RangosPeso.AsNoTracking().ToListAsync(ct);

    public Task<clsRangoPeso?> ObtenerAsync(int id, CancellationToken ct) =>
        db.RangosPeso.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ExisteAsync(int pesoMinimo, int? pesoMaximo, int? excluirId, CancellationToken ct) =>
        db.RangosPeso.AnyAsync(r =>
            r.PesoMinimo == pesoMinimo && r.PesoMaximo == pesoMaximo && (excluirId == null || r.Id != excluirId), ct);

    public void Agregar(clsRangoPeso rango) => db.RangosPeso.Add(rango);

    public async Task<int> EliminarAsync(int id, CancellationToken ct) =>
        await db.RangosPeso.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
}
