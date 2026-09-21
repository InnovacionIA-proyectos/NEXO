using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Catálogo de prospectos (~dbSolucionesTI.tblProspectos), cacheado 1 h. Configurado=false
/// cuando ConnectionStrings:DbSolucionesTi no está definida (DbSolucionesTiDbContext no se
/// registra): la búsqueda de clientes simplemente no cae a esta fuente, nunca falla.
/// </summary>
public sealed class ProspectosDwAdapter(IServiceProvider sp, IMemoryCache cache) : IProspectosDwPort
{
    private readonly DbSolucionesTiDbContext? _db = sp.GetService<DbSolucionesTiDbContext>();

    public bool Configurado => _db is not null;

    public async Task<IReadOnlyList<ProspectoDwDto>> ListarAsync(CancellationToken ct)
    {
        if (_db is null) return [];
        var lista = await cache.GetOrCreateAsync("captura:prospectos-dw", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var filas = await _db.Prospectos.Select(p => new { p.Nit, p.Cliente }).ToListAsync(ct);
            return filas
                .Where(f => !string.IsNullOrWhiteSpace(f.Cliente))
                .Select(f => new ProspectoDwDto(f.Nit.Trim(), Limpiar(f.Cliente)!))
                .ToList();
        });
        return lista!;
    }

    private static string? Limpiar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var colapsado = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return System.Text.RegularExpressions.Regex.Replace(colapsado, @"\s+([.,])", "$1");
    }
}
