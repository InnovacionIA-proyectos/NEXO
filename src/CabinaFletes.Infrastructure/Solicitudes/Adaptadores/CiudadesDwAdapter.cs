using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Domain.Common;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Solicitudes.Adaptadores;

/// <summary>
/// Catálogo de municipios "costeables" (tblSAPDANE filtrado por <see cref="ResolucionZonaSap"/>),
/// cacheado 1 h. Configurado=false cuando ConnectionStrings:DbSolucionesTi no está
/// definida: la captura no ofrece este catálogo, pero no falla (HU-005 parte 3).
/// </summary>
public sealed class CiudadesDwAdapter(IServiceProvider sp, IMemoryCache cache) : ICiudadesDwPort
{
    private readonly DbSolucionesTiDbContext? _db = sp.GetService<DbSolucionesTiDbContext>();

    public bool Configurado => _db is not null;

    public async Task<IReadOnlyList<CiudadDwDto>> ListarAsync(CancellationToken ct)
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

            var resultado = new List<CiudadDwDto>();
            var vistos = new HashSet<string>();

            foreach (var m in municipios)
            {
                var municipio = Texto.Limpiar(m.NombreMunicipio);
                if (municipio is null) continue;
                if (!ResolucionZonaSap.Resuelve(Texto.Normalizar(municipio), zonasValidas)) continue;

                var departamento = Texto.Limpiar(m.NombreDepartamento) ?? "";
                if (vistos.Add($"{municipio}|{departamento}"))
                    resultado.Add(new CiudadDwDto(municipio, departamento));
            }

            foreach (var alias in ResolucionZonaSap.AliasZonasAgregadas.Values)
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
            .Select(c => ResolucionZonaSap.SinPrefijoZona(Texto.Normalizar(c)))
            .SelectMany(ResolucionZonaSap.ExpandirAbreviaturas)
            .Distinct()
            .ToList();
    }
}
