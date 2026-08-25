using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Solicitudes.Repositorios;

public sealed class EfClientesRepository(CabinaFletesDbContext db) : IClientesPort
{
    public Task<bool> ExisteAsync(Guid id, CancellationToken ct) => db.Clientes.AnyAsync(c => c.Id == id, ct);

    public Task<Cliente?> ObtenerPorNitAsync(string nit, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Nit == nit, ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);

    public async Task<IReadOnlyList<Cliente>> BuscarAsync(string? terminoNormalizado, int top, CancellationToken ct)
    {
        var query = db.Clientes.AsNoTracking();
        if (!string.IsNullOrEmpty(terminoNormalizado))
            query = query.Where(c => c.Nombre.ToUpper().Contains(terminoNormalizado) || c.Nit.Contains(terminoNormalizado));
        return await query.OrderBy(c => c.Nombre).Take(top).ToListAsync(ct);
    }
}
