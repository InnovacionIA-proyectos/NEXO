using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class EfClientesRepository(NexoDbContext db) : IClientesPort
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
