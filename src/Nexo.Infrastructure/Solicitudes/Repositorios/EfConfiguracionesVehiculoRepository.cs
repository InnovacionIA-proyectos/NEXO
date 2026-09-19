using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class EfConfiguracionesVehiculoRepository(NexoDbContext db) : IConfiguracionesVehiculoPort
{
    public async Task<IReadOnlyList<ConfiguracionVehiculo>> ListarAsync(CancellationToken ct) =>
        await db.ConfiguracionesVehiculo.ToListAsync(ct);

    public void Agregar(ConfiguracionVehiculo vehiculo) => db.ConfiguracionesVehiculo.Add(vehiculo);
}
