using Microsoft.EntityFrameworkCore;
using Nexo.Application.CasosDeUso.LecturaExcel.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Recetas de lectura de Excel por cliente sobre <c>tbl_recetas_lectura_cliente</c>.</summary>
public sealed class clsEfRecetasLecturaRepository(clsNexoDbContext db) : IRecetasLecturaRepository
{
    public async Task<IReadOnlyList<clsRecetaLecturaCliente>> ListarAsync(Guid? clienteId, CancellationToken ct)
    {
        var q = db.RecetasLectura.AsNoTracking().Include(r => r.Cliente).AsQueryable();
        if (clienteId is { } id) q = q.Where(r => r.ClienteId == id);
        return await q.OrderBy(r => r.Cliente!.Nombre).ThenByDescending(r => r.UltimoUsoAt ?? r.UpdatedAt ?? r.CreatedAt).ToListAsync(ct);
    }

    public Task<clsRecetaLecturaCliente?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.RecetasLectura.FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct) =>
        db.Clientes.AsNoTracking().AnyAsync(c => c.Id == clienteId, ct);

    public void Agregar(clsRecetaLecturaCliente receta) => db.RecetasLectura.Add(receta);

    public void Eliminar(clsRecetaLecturaCliente receta) => db.RecetasLectura.Remove(receta);
}
