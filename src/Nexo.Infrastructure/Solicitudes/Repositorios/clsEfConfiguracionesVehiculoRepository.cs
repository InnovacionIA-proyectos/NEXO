using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfConfiguracionesVehiculoRepository(clsNexoDbContext db) : IConfiguracionesVehiculoPort
{
    public async Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.ToListAsync(ct);

    public Task<clsConfiguracionVehiculo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.ConfiguracionesVehiculo.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarEnCuarentenaAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.AsNoTracking()
            .Where(v => !v.EsCatalogoOficial && !v.Descartada)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.TipoVehiculo).ThenBy(v => v.Carroceria)
            .ToListAsync(ct);

    public void Agregar(clsConfiguracionVehiculo vehiculo) => db.ConfiguracionesVehiculo.Add(vehiculo);
}
