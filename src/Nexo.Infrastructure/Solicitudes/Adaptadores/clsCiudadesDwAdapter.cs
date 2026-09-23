using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Domain.Common;
using Nexo.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Catálogo de municipios "costeables" (tblSAPDANE filtrado por <see cref="clsResolucionZonaSap"/>),
/// cacheado 1 h. Configurado=false cuando ConnectionStrings:DbSolucionesTi no está
/// definida: la captura no ofrece este catálogo, pero no falla.
/// </summary>
public sealed class clsCiudadesDwAdapter(IServiceProvider sp, IMemoryCache cache) : ICiudadesDwPort
{
    private readonly clsDbSolucionesTiDbContext? _db = sp.GetService<clsDbSolucionesTiDbContext>();

    public bool Configurado => _db is not null;

    public async Task<IReadOnlyList<clsCiudadDwDto>> ListarAsync(CancellationToken ct)
    {
        if (_db is null) return [];
        var lista = await cache.GetOrCreateAsync("captura:ciudades-dw", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            var zonasValidas = await ZonasValidasAsync(ct);

            var municipios = await _db.CentrosPobladosDane
                .Select(d => new { d.NombreMunicipio, d.NombreDepartamento })
                .Distinct()
                .ToListAsync(ct);

            var resultado = new List<clsCiudadDwDto>();
            var vistos = new HashSet<string>();

            foreach (var m in municipios)
            {
                var municipio = clsTexto.Limpiar(m.NombreMunicipio);
                if (municipio is null) continue;
                if (!clsResolucionZonaSap.Resuelve(clsTexto.Normalizar(municipio), zonasValidas)) continue;

                var departamento = clsTexto.Limpiar(m.NombreDepartamento) ?? "";
                if (vistos.Add($"{municipio}|{departamento}"))
                    resultado.Add(new clsCiudadDwDto(municipio, departamento));
            }

            foreach (var alias in clsResolucionZonaSap.AliasZonasAgregadas.Values)
                if (vistos.Add($"{alias.Municipio}|{alias.Departamento}"))
                    resultado.Add(alias);

            return resultado;
        });
        return lista!;
    }

    private async Task<List<string>> ZonasValidasAsync(CancellationToken ct)
    {
        var origenes = await _db!.TarifasPlazaSap.Select(t => t.Origen).Distinct().ToListAsync(ct);
        var destinos = await _db.TarifasPlazaSap.Select(t => t.Destino).Distinct().ToListAsync(ct);
        var zonasTransporte = await _db.ZonasTarificacion.Select(z => z.IdZonaTransporte).Distinct().ToListAsync(ct);

        return origenes.Concat(destinos).Concat(zonasTransporte)
            .Select(c => clsResolucionZonaSap.SinPrefijoZona(clsTexto.Normalizar(c)))
            .SelectMany(clsResolucionZonaSap.ExpandirAbreviaturas)
            .Distinct()
            .ToList();
    }
}
