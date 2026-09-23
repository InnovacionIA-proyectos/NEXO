using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Catálogo de clientes reales (~4,5 K filas) del DW, cacheado 1 h. Configurado=false
/// cuando ConnectionStrings:DwCubos no está definida (clsDwCubosDbContext no se registra):
/// la captura se degrada a la tabla local de clientes, nunca falla.
/// </summary>
public sealed class clsClientesDwAdapter(IServiceProvider sp, IMemoryCache cache) : IClientesDwPort
{
    private readonly clsDwCubosDbContext? _dw = sp.GetService<clsDwCubosDbContext>();

    public bool Configurado => _dw is not null;

    public async Task<IReadOnlyList<clsClienteDwDto>> ListarAsync(CancellationToken ct)
    {
        if (_dw is null) return [];
        var lista = await cache.GetOrCreateAsync("captura:clientes-dw", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var filas = await _dw.Solicitantes.Select(s => new { s.Bp, s.Nit, s.Nombre }).ToListAsync(ct);
            return filas
                .Where(f => !string.IsNullOrWhiteSpace(f.Nombre))
                .Select(f => new clsClienteDwDto(f.Bp.Trim(), f.Nit.Trim(), Limpiar(f.Nombre)!))
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
