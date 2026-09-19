using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfConfiguracionesVehiculoRepository(clsNexoDbContext db) : IConfiguracionesVehiculoPort
{
    public async Task<IReadOnlyList<clsConfiguracionVehiculo>> ListarAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.ToListAsync(ct);

    public void Agregar(clsConfiguracionVehiculo vehiculo) => db.ConfiguracionesVehiculo.Add(vehiculo);
}
