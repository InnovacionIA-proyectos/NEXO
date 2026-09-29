using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Catalogos.Adaptadores;

/// <summary>Línea de negocio (tblSAPLineaNegocio, dbSolucionesTI) — mismo patrón de caché 1h que <see cref="clsTiposVehiculoAdapter"/>.</summary>
public sealed class clsLineasNegocioAdapter(IServiceProvider sp, IMemoryCache cache) : ILineasNegocioPort
{
    private readonly clsDbSolucionesTiDbContext? _dc = sp.GetService<clsDbSolucionesTiDbContext>();

    public async Task<IReadOnlyList<clsLineaNegocioDto>> ListarAsync(CancellationToken ct)
    {
        if (_dc is null) return [];
        return (await cache.GetOrCreateAsync("catalogos:lineas-negocio", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _dc.LineasNegocio.AsNoTracking()
                .Select(l => new clsLineaNegocioDto(l.Id, l.Descripcion.Trim()))
                .ToListAsync(ct);
        }))!;
    }
}

/// <summary>Clase de viaje (tblSAPClaseViaje, dbSolucionesTI).</summary>
public sealed class clsClasesViajeAdapter(IServiceProvider sp, IMemoryCache cache) : IClasesViajePort
{
    private readonly clsDbSolucionesTiDbContext? _dc = sp.GetService<clsDbSolucionesTiDbContext>();

    public async Task<IReadOnlyList<clsClaseViajeDto>> ListarAsync(CancellationToken ct)
    {
        if (_dc is null) return [];
        return (await cache.GetOrCreateAsync("catalogos:clases-viaje", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _dc.ClasesViaje.AsNoTracking()
                .Select(c => new clsClaseViajeDto(c.Id, c.Descripcion.Trim()))
                .ToListAsync(ct);
        }))!;
    }
}

/// <summary>Clase de contenedor (tblSAPClaseContenedor, dbSolucionesTI) — incluye tamaño y tara para
/// que el formulario de Tarifas de Plaza sugiera el rango de peso al elegir la clase.</summary>
public sealed class clsClasesContenedorAdapter(IServiceProvider sp, IMemoryCache cache) : IClasesContenedorPort
{
    private readonly clsDbSolucionesTiDbContext? _dc = sp.GetService<clsDbSolucionesTiDbContext>();

    public async Task<IReadOnlyList<clsClaseContenedorDto>> ListarAsync(CancellationToken ct)
    {
        if (_dc is null) return [];
        return (await cache.GetOrCreateAsync("catalogos:clases-contenedor", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            return await _dc.ClasesContenedor.AsNoTracking()
                .Select(c => new clsClaseContenedorDto(c.Id, c.Descripcion.Trim(), c.TamanoContenedor, c.PesoInfCnt, c.PesoSupCnt))
                .ToListAsync(ct);
        }))!;
    }
}
