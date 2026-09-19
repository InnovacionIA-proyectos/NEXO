using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.PlanDeCargue.Repositorios;

/// <summary>S3-HU-03 (issue #112): lee la proyección de vehículos que
/// VehiculoCatalogoConsumer/PlanDeCargueGeneradoConsumer mantienen sincronizada
/// (issue #111) — nunca llama a Cubicador.</summary>
public sealed class EfVehiculoCubicacionRepository(CabinaFletesDbContext db) : IVehiculoCubicacionRepository
{
    public async Task<(IReadOnlyList<VehiculoCubicacion> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.VehiculosCubicacion.AsNoTracking().OrderBy(v => v.Nombre);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<VehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.VehiculosCubicacion.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
}
