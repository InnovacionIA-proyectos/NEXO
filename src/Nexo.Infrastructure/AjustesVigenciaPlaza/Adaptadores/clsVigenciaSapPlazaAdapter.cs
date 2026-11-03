using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.AjustesVigenciaPlaza.Adaptadores;

/// <summary>Vigencia SAP de las tarifas de plaza (clsDbSolucionesTiDbContext, SOLO LECTURA, opcional).</summary>
public sealed class clsVigenciaSapPlazaAdapter(IServiceProvider sp) : IVigenciaSapPlazaPort
{
    private readonly clsDbSolucionesTiDbContext? _db = sp.GetService<clsDbSolucionesTiDbContext>();

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
