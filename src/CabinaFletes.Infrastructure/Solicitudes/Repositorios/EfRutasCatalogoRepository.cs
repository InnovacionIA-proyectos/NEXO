using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Solicitudes.Repositorios;

public sealed class EfRutasCatalogoRepository(CabinaFletesDbContext db) : IRutasCatalogoPort
{
    public async Task<IReadOnlyList<Ruta>> ListarAsync(CancellationToken ct) => await db.Rutas.ToListAsync(ct);

    public void Agregar(Ruta ruta) => db.Rutas.Add(ruta);
}
