using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Solicitudes.Repositorios;

public sealed class EfConfiguracionesVehiculoRepository(CabinaFletesDbContext db) : IConfiguracionesVehiculoPort
{
    public async Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.ToListAsync(ct);

    public void Agregar(ConfiguracionVehiculo vehiculo) => db.ConfiguracionesVehiculo.Add(vehiculo);
}
