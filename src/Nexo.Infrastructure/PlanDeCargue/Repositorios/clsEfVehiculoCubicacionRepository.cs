using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.PlanDeCargue.Repositorios;

/// <summary>S3-HU-03 (issue #112): lee la proyección de vehículos que
/// clsVehiculoCatalogoConsumer/clsPlanDeCargueGeneradoConsumer mantienen sincronizada
/// (issue #111) — nunca llama a Cubicador.</summary>
public sealed class clsEfVehiculoCubicacionRepository(clsNexoDbContext db) : IVehiculoCubicacionRepository
{
    public async Task<(IReadOnlyList<clsVehiculoCubicacion> Items, int Total)> ListarAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = db.VehiculosCubicacion.AsNoTracking().OrderBy(v => v.Nombre);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<clsVehiculoCubicacion?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.VehiculosCubicacion.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
}
