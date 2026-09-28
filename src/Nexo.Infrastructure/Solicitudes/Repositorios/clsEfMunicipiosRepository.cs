using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Solicitudes.Repositorios;

public sealed class clsEfMunicipiosRepository(clsNexoDbContext db) : IMunicipiosRepository
{
    public Task<bool> ExisteAlgunoAsync(CancellationToken ct) => db.Municipios.AnyAsync(ct);

    public void AgregarRango(IEnumerable<clsMunicipio> municipios) => db.Municipios.AddRange(municipios);

    public async Task<IReadOnlyList<clsMunicipio>> ListarAsync(CancellationToken ct) =>
        await db.Municipios.AsNoTracking().OrderBy(m => m.Municipio).ToListAsync(ct);
}
