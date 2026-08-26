using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.AjustesVigenciaPlaza.Adaptadores;

/// <summary>Vigencia SAP de las tarifas de plaza (DbSolucionesTiDbContext, SOLO LECTURA, opcional).</summary>
public sealed class VigenciaSapPlazaAdapter(IServiceProvider sp) : IVigenciaSapPlazaPort
{
    private readonly DbSolucionesTiDbContext? _db = sp.GetService<DbSolucionesTiDbContext>();

    public bool Configurado => _db is not null;

    public async Task<IReadOnlyDictionary<int, DateTime>> VigenciasAsync(IReadOnlyCollection<int> tarifaPlazaIds, CancellationToken ct)
    {
        if (_db is null || tarifaPlazaIds.Count == 0) return new Dictionary<int, DateTime>();
        try
        {
            return await _db.TarifasPlazaSap.AsNoTracking()
                .Where(t => tarifaPlazaIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Vigencia })
                .ToDictionaryAsync(x => x.Id, x => x.Vigencia, ct);
        }
        catch
        {
            // La lectura del DW es best-effort: nunca bloquea el ajuste local por un DW caído.
            return new Dictionary<int, DateTime>();
        }
    }
}
