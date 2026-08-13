using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Entities;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Repositories;

public sealed class EfConfiguracionesVehiculoRepository(CabinaFletesDbContext db) : IConfiguracionesVehiculoPort
{
    public async Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.ToListAsync(ct);

    public void Agregar(ConfiguracionVehiculo vehiculo) => db.ConfiguracionesVehiculo.Add(vehiculo);
}
