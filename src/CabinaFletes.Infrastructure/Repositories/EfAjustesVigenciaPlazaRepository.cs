using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using CabinaFletes.Domain.Costeo.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CabinaFletes.Infrastructure.Repositories;

public sealed class EfAjustesVigenciaPlazaRepository(CabinaFletesDbContext db, IMemoryCache cache) : IAjustesVigenciaPlazaRepository
{
    private const string CacheKeyMapa = "ajustes-vigencia-plaza:mapa";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyDictionary<int, DateTime>> MapaAsync(CancellationToken ct) =>
        await cache.GetOrCreateAsync(CacheKeyMapa, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            try
            {
                return (IReadOnlyDictionary<int, DateTime>)await db.AjustesVigenciaPlaza.AsNoTracking()
                    .ToDictionaryAsync(a => a.TarifaPlazaId, a => a.VigenciaAjustada, ct);
            }
            catch
            {
                // Nunca romper el costeo por un fallo leyendo overrides.
                return new Dictionary<int, DateTime>();
            }
        }) ?? new Dictionary<int, DateTime>();

    public Task<AjusteVigenciaPlaza?> ObtenerPorTarifaAsync(int tarifaPlazaId, CancellationToken ct) =>
        db.AjustesVigenciaPlaza.FirstOrDefaultAsync(a => a.TarifaPlazaId == tarifaPlazaId, ct);

    public async Task<IReadOnlyDictionary<int, AjusteVigenciaPlaza>> ObtenerPorTarifasAsync(
        IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct) =>
        (await db.AjustesVigenciaPlaza.Where(a => tarifaPlazaIds.Contains(a.TarifaPlazaId)).ToListAsync(ct))
            .ToDictionary(a => a.TarifaPlazaId);

    public void Agregar(AjusteVigenciaPlaza ajuste) => db.AjustesVigenciaPlaza.Add(ajuste);

    public void Eliminar(AjusteVigenciaPlaza ajuste) => db.AjustesVigenciaPlaza.Remove(ajuste);

    public void InvalidarCache() => cache.Remove(CacheKeyMapa);
}
