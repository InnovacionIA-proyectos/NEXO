using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfClientesRepository(clsNexoDbContext db) : IClientesPort
{
    public Task<bool> ExisteAsync(Guid id, CancellationToken ct) => db.Clientes.AnyAsync(c => c.Id == id, ct);

    // Issue #206: el NIT que llega del catálogo DW nunca trae dígito de verificación
    // ("900123456"), pero algunos registros locales sí lo tienen ("900123456-3"). Sin
    // este OR, un cliente ya existente localmente con DV nunca matchea y se duplica.
    public Task<clsCliente?> ObtenerPorNitAsync(string nit, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Nit == nit || EF.Functions.Like(c.Nit, nit + "-%"), ct);

    public void Agregar(clsCliente cliente) => db.Clientes.Add(cliente);

    public async Task<IReadOnlyList<clsCliente>> BuscarAsync(string? terminoNormalizado, int top, CancellationToken ct)
    {
        var query = db.Clientes.AsNoTracking();
        if (!string.IsNullOrEmpty(terminoNormalizado))
            query = query.Where(c => c.Nombre.ToUpper().Contains(terminoNormalizado) || c.Nit.Contains(terminoNormalizado));
        return await query.OrderBy(c => c.Nombre).Take(top).ToListAsync(ct);
    }
}
