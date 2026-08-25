using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Repositories;

public sealed class EfRutasCatalogoRepository(CabinaFletesDbContext db) : IRutasCatalogoPort
{
    public async Task<IReadOnlyList<Ruta>> ListarAsync(CancellationToken ct) => await db.Rutas.ToListAsync(ct);

    public void Agregar(Ruta ruta) => db.Rutas.Add(ruta);
}
