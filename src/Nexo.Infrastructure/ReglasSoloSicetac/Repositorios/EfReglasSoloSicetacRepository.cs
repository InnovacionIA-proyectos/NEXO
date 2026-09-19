using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Domain.Costeo.Entidades;
using Nexo.Infrastructure.Persistence;
using Nexo.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Nexo.Infrastructure.ReglasSoloSicetac.Repositorios;

public sealed class EfReglasSoloSicetacRepository(NexoDbContext db, IMemoryCache cache)
    : IReglasSoloSicetacRepository, IClavesSoloSicetacPort
{
    private const string CacheKeyClaves = "reglas-solo-sicetac:claves-ruta-vehiculo";
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
    /// Claves por RUTA + TIPO DE VEHÍCULO en la forma canónica de Domain — puerto de
    /// lectura para el motor de propuesta de tarifa (RN-01).
    ///
    /// Issue #17: antes proyectaba solo Origen/Destino, descartando TipoVehiculo -
    /// marcar un único tipo de vehículo como excepción apagaba la comparación contra
    /// plaza para TODOS los tipos de esa ruta. Se reconstruye con Texto.ClaveRutaVehiculo
    /// en vez de concatenar lo almacenado: aunque hoy se guarda ya normalizado,
    /// reconstruirla hace la coincidencia inmune a una fila vieja o cargada por otra vía.
    /// </summary>
    public async Task<IReadOnlySet<string>> ClavesRutaVehiculoAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(CacheKeyClaves, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var filas = await db.ReglasSoloSicetac.AsNoTracking().Select(r => new { r.Origen, r.Destino, r.TipoVehiculo }).ToListAsync(ct);
            return (IReadOnlySet<string>)filas.Select(r => Texto.ClaveRutaVehiculo(r.Origen, r.Destino, r.TipoVehiculo)).ToHashSet();
        }))!;
}
