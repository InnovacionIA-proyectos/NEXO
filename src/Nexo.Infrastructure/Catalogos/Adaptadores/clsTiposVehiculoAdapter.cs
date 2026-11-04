using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Solo tblSAPTiposVehiculoTarifario (dbSolucionesTI), tal cual, sin valores a mano (los códigos 3S2 y 2L3 faltan en SAP y no se hardcodean).
/// Si dbSolucionesTI no está disponible devuelve una lista vacía, nunca un catálogo inventado.</summary>
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
