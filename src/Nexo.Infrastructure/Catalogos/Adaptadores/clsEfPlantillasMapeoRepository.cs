using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Nexo.Application.CasosDeUso.PlantillasMapeo.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Plantillas de mapeo de columnas por cliente sobre <c>tbl_plantillas_mapeo_cliente</c>.</summary>
public sealed class clsEfPlantillasMapeoRepository(clsNexoDbContext db) : IPlantillasMapeoRepository
{
    public async Task<IReadOnlyList<clsPlantillaMapeoCliente>> ListarAsync(Guid? clienteId, CancellationToken ct)
    {
        var q = db.PlantillasMapeo.AsNoTracking().Include(p => p.Cliente).AsQueryable();
        if (clienteId is { } id) q = q.Where(p => p.ClienteId == id);
        return await q.OrderBy(p => p.Cliente!.Nombre).ThenByDescending(p => p.UpdatedAt ?? p.CreatedAt).ToListAsync(ct);
    }

    public Task<clsPlantillaMapeoCliente?> BuscarPorFirmaAsync(Guid clienteId, string firma, CancellationToken ct) =>
        db.PlantillasMapeo.AsNoTracking().Include(p => p.Cliente)
            .FirstOrDefaultAsync(p => p.ClienteId == clienteId && p.FirmaEncabezados == firma, ct);

    public Task<clsPlantillaMapeoCliente?> ObtenerParaActualizarAsync(Guid clienteId, string firma, CancellationToken ct) =>
        db.PlantillasMapeo.FirstOrDefaultAsync(p => p.ClienteId == clienteId && p.FirmaEncabezados == firma, ct);

    public Task<clsPlantillaMapeoCliente?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.PlantillasMapeo.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ClienteExisteAsync(Guid clienteId, CancellationToken ct) =>
        db.Clientes.AsNoTracking().AnyAsync(c => c.Id == clienteId, ct);

    public void Agregar(clsPlantillaMapeoCliente plantilla) => db.PlantillasMapeo.Add(plantilla);

    public void Eliminar(clsPlantillaMapeoCliente plantilla) => db.PlantillasMapeo.Remove(plantilla);

    // 2601 y 2627: violación de un índice único / de una restricción única.
    public bool EsChoqueDeFirma(Exception error) =>
        error is DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } };

    public void DescartarCambiosPendientes() => db.ChangeTracker.Clear();
}
