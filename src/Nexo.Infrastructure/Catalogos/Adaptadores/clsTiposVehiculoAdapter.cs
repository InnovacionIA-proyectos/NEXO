using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>
/// Catálogo de tipos de vehículo: solo tblSAPTiposVehiculoTarifario (dbSolucionesTI), tal cual.
///
/// S5-HU-22.1/#288: antes se le sumaban 5 valores escritos a mano (CNT20/Consolidado,
/// CNT20/Exclusivo, CNT40/Consolidado, CNT40/Exclusivo, PBV 7500-8000) para cubrir casos que el
/// catálogo de SAP no traía. Negocio confirmó que ya no hacen falta y se ELIMINARON (no se
/// movieron a una tabla). Las reglas ya guardadas con alguno de esos códigos no se tocan.
///
/// Los códigos 3S2 y 2L3 siguen faltando en el catálogo de SAP: decisión de negocio, se deja así
/// por ahora — no se hardcodean.
///
/// Si dbSolucionesTI no está disponible devuelve una lista vacía (nunca un catálogo inventado).
/// </summary>
public sealed class clsTiposVehiculoAdapter(IServiceProvider sp, IMemoryCache cache) : ITiposVehiculoPort
{
    private readonly clsDbSolucionesTiDbContext? _dc = sp.GetService<clsDbSolucionesTiDbContext>();

    public async Task<IReadOnlyList<clsTipoVehiculoDto>> ListarAsync(CancellationToken ct)
    {
        if (_dc is null) return [];

        var oficiales = await cache.GetOrCreateAsync("catalogos:tipos-vehiculo", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _dc.TiposVehiculo.AsNoTracking()
                .Where(v => v.Estado)
                .Select(v => new clsTipoVehiculoDto(v.Codigo.Trim(), v.Descripcion.Trim(), true))
                .ToListAsync(ct);
        });

        return oficiales!;
    }
}
