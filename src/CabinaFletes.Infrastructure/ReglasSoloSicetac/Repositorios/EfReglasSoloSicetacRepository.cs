using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using CabinaFletes.Domain.Costeo.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CabinaFletes.Infrastructure.ReglasSoloSicetac.Repositorios;

public sealed class EfReglasSoloSicetacRepository(CabinaFletesDbContext db, IMemoryCache cache)
    : IReglasSoloSicetacRepository, IClavesSoloSicetacPort
{
    private const string CacheKeyClaves = "reglas-solo-sicetac:claves";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<ReglaSoloSicetac>> ListarAsync(CancellationToken ct) =>
        await db.ReglasSoloSicetac.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlySet<string>> ClavesOrigenDestinoTipoVehiculoAsync(CancellationToken ct) =>
        (await db.ReglasSoloSicetac.AsNoTracking().Select(r => new { r.Origen, r.Destino, r.TipoVehiculo }).ToListAsync(ct))
            .Select(r => $"{r.Origen}|{r.Destino}|{r.TipoVehiculo}").ToHashSet();

    public Task<bool> ExisteAsync(string origen, string destino, string tipoVehiculo, CancellationToken ct) =>
        db.ReglasSoloSicetac.AnyAsync(r => r.Origen == origen && r.Destino == destino && r.TipoVehiculo == tipoVehiculo, ct);

    public void Agregar(ReglaSoloSicetac regla)
    {
        db.ReglasSoloSicetac.Add(regla);
        cache.Remove(CacheKeyClaves);
    }

    public void AgregarRango(IEnumerable<ReglaSoloSicetac> reglas)
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

    /// <summary>
    /// Claves por RUTA (sin el vehículo) en la forma canónica de Domain — puerto de
    /// lectura para Biblioteca.
    ///
    /// Se reconstruye con Texto.ClaveRuta en vez de concatenar lo almacenado: aunque hoy
    /// se guarda ya normalizado, reconstruirla hace la coincidencia inmune a una fila
    /// vieja o cargada por otra vía.
    /// </summary>
    public async Task<IReadOnlySet<string>> ClavesRutaAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheKeyClaves, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var filas = await db.ReglasSoloSicetac.AsNoTracking().Select(r => new { r.Origen, r.Destino }).ToListAsync(ct);
            return (IReadOnlySet<string>)filas.Select(r => Texto.ClaveRuta(r.Origen, r.Destino)).ToHashSet();
        }))!;
}
