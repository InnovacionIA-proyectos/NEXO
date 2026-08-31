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
}
