using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Infrastructure.Persistence;
using Nexo.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Nexo.Infrastructure.ReglasSoloSicetac.Repositorios;

public sealed class clsEfReglasSoloSicetacRepository(clsNexoDbContext db, IMemoryCache cache)
    : IReglasSoloSicetacRepository, IClavesSoloSicetacPort
{
    private const string CacheKeyClaves = "reglas-solo-sicetac:claves-ruta-vehiculo";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<clsReglaSoloSicetac>> ListarAsync(CancellationToken ct) =>
        await db.ReglasSoloSicetac.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlySet<string>> ClavesOrigenDestinoTipoVehiculoAsync(CancellationToken ct) =>
        (await db.ReglasSoloSicetac.AsNoTracking().Select(r => new { r.Origen, r.Destino, r.TipoVehiculo }).ToListAsync(ct))
            .Select(r => $"{r.Origen}|{r.Destino}|{r.TipoVehiculo}").ToHashSet();

    public Task<bool> ExisteAsync(string origen, string destino, string tipoVehiculo, CancellationToken ct) =>
        db.ReglasSoloSicetac.AnyAsync(r => r.Origen == origen && r.Destino == destino && r.TipoVehiculo == tipoVehiculo, ct);

    public void Agregar(clsReglaSoloSicetac regla)
    {
        db.ReglasSoloSicetac.Add(regla);
        cache.Remove(CacheKeyClaves);
    }

    public void AgregarRango(IEnumerable<clsReglaSoloSicetac> reglas)
    {
        db.ReglasSoloSicetac.AddRange(reglas);
        cache.Remove(CacheKeyClaves);
    }

    public async Task<int> EliminarAsync(Guid id, CancellationToken ct)
    {
        var borradas = await db.ReglasSoloSicetac.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        if (borradas > 0) cache.Remove(CacheKeyClaves);
        return borradas;
    }

    /// <summary>Claves por RUTA + TIPO DE VEHÍCULO en forma canónica (puerto de lectura del motor de propuesta). Se reconstruyen con
    /// clsTexto.ClaveRutaVehiculo en vez de concatenar lo almacenado, para ser inmunes a filas viejas o cargadas por otra vía.</summary>
    public async Task<IReadOnlySet<string>> ClavesRutaVehiculoAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheKeyClaves, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var filas = await db.ReglasSoloSicetac.AsNoTracking().Select(r => new { r.Origen, r.Destino, r.TipoVehiculo }).ToListAsync(ct);
            return (IReadOnlySet<string>)filas.Select(r => clsTexto.ClaveRutaVehiculo(r.Origen, r.Destino, r.TipoVehiculo)).ToHashSet();
        }))!;
}
