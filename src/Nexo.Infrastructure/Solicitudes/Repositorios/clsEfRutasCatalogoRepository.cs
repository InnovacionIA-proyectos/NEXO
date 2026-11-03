using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfRutasCatalogoRepository(clsNexoDbContext db) : IRutasCatalogoPort
{
    public async Task<IReadOnlyList<clsRuta>> ListarAsync(CancellationToken ct) => await db.Rutas.ToListAsync(ct);

    public void Agregar(clsRuta ruta) => db.Rutas.Add(ruta);
}
