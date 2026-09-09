using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.PlanDeCargue.Repositorios;

public sealed class EfPlanCargueRepository(CabinaFletesDbContext db) : IPlanCargueRepository
{
    public Task<PlanCargue?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.PlanesCargue
            .Include(p => p.Items)
            .Include(p => p.Colocaciones)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task PrepararCreacionAsync(PlanCargue plan, CancellationToken ct)
    {
        var vehiculoExiste = await db.VehiculosCubicacion.AnyAsync(v => v.Id == plan.VehiculoId, ct);
        if (!vehiculoExiste)
            db.VehiculosCubicacion.Add(new VehiculoCubicacion { Id = plan.VehiculoId, Nombre = plan.VehiculoId.ToString() });

        db.PlanesCargue.Add(plan);
    }

    public async Task EliminarSiExisteAsync(Guid id, CancellationToken ct)
    {
        var fila = await db.PlanesCargue.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (fila is not null)
            db.PlanesCargue.Remove(fila);
    }
}
