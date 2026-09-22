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
            .Include(p => p.Vehiculo)
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

    public async Task<(IReadOnlyList<PlanCargue> Items, int Total)> ListarRecientesAsync(int page, int pageSize, CancellationToken ct)
    {
        // Include de Items/Colocaciones porque ListarPlanesCargueQueryHandler necesita
        // sus conteos (TotalItems/TotalColocaciones) - correcto pero no lo más liviano
        // posible; si el listado se vuelve lento, la palanca es proyectar con
        // Items.Count()/Colocaciones.Count() en el SELECT en vez de traer las filas.
        var query = db.PlanesCargue.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(p => p.Items)
            .Include(p => p.Colocaciones)
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<PlanCargue>> ObtenerFlotaAsync(Guid planId, CancellationToken ct)
    {
        var plan = await db.PlanesCargue.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null)
            return Array.Empty<PlanCargue>();

        IQueryable<PlanCargue> hermanos = plan.LineaSolicitudId is null
            ? db.PlanesCargue.Where(p => p.Id == planId)
            : db.PlanesCargue.Where(p => p.LineaSolicitudId == plan.LineaSolicitudId);

        return await hermanos
            .Include(p => p.Colocaciones)
            .OrderBy(p => p.NumeroVehiculo)
            .ToListAsync(ct);
    }

    /// <summary>[S3-HU-04.3, issue #78] Ver la firma en IPlanCargueRepository — a
    /// diferencia de <see cref="ObtenerFlotaAsync"/>, no parte de un PlanId existente:
    /// filtra directo por LineaSolicitudId, así que también sirve mientras ningún plan de
    /// la línea ha llegado todavía (lista vacía, no falla).</summary>
    public async Task<IReadOnlyList<PlanCargue>> ObtenerPorLineaAsync(Guid lineaSolicitudId, CancellationToken ct) =>
        await db.PlanesCargue
            .AsNoTracking()
            .Where(p => p.LineaSolicitudId == lineaSolicitudId)
            .Include(p => p.Colocaciones)
            .OrderBy(p => p.NumeroVehiculo)
            .ToListAsync(ct);

    public Task<bool> ExisteConVehiculoAsync(Guid vehiculoId, CancellationToken ct) =>
        db.PlanesCargue.AsNoTracking().AnyAsync(p => p.VehiculoId == vehiculoId, ct);
}
