using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.DTOs;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico.Interfaces;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Consulta de SOLO LECTURA a las tarifas SAP TM del datacenter corporativo: el
/// histórico sale del log de cambios (dbsolucionesTI.dbo.tblSAPTarifasLog, ~24 K
/// filas) y las de plaza de dbo.tblSAPTarifas. Se ordena por vigencia descendente
/// y los catálogos/filtros se cachean. Portado 1:1 desde
/// CabinaFletes.Api/Services/BibliotecaSapHistoricoService.cs del proyecto
/// original; el ajuste local de vigencia (antes IAjusteVigenciaPlazaService) se
/// resuelve aquí con IAjustesVigenciaPlazaRepository.MapaAsync (mismo módulo que
/// ya migró la escritura de esos overrides).
/// </summary>
public sealed class SapHistoricoAdapter(
    DbSolucionesTiDbContext db, IMemoryCache cache, IAjustesVigenciaPlazaRepository ajustes) : ISapHistoricoPort
{
    private const int MaxTop = 200;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly IReadOnlyDictionary<int, DateTime> SinAjustes = new Dictionary<int, DateTime>();

    public Task<SapHistoricoResultDto> BuscarAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default)
        // El histórico se identifica por Id de log (no de tarifa de plaza): sin ajustes.
        => BuscarCoreAsync(db.TarifasLogSap, SinAjustes, q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top, ct);

    public async Task<SapHistoricoResultDto> BuscarPlazaAsync(
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct = default)
    {
        var mapaAjustes = await ajustes.MapaAsync(ct);
        return await BuscarCoreAsync(db.TarifasPlazaSap, mapaAjustes, q, lineaNegocio, tipoVehiculo, tipoCarroseria, claseViaje, top, ct);
    }

    public Task<SapHistoricoFiltrosDto> FiltrosAsync(CancellationToken ct = default)
        => FiltrosCoreAsync(db.TarifasLogSap, "saplog:filtros", ct);

    public Task<SapHistoricoFiltrosDto> FiltrosPlazaAsync(CancellationToken ct = default)
        => FiltrosCoreAsync(db.TarifasPlazaSap, "sapplaza:filtros", ct);

    private async Task<SapHistoricoResultDto> BuscarCoreAsync<T>(
        IQueryable<T> origen, IReadOnlyDictionary<int, DateTime> ajustesVigencia,
        string? q, int? lineaNegocio, string? tipoVehiculo, string? tipoCarroseria, int? claseViaje,
        int top, CancellationToken ct) where T : SapTarifaBase
    {
        top = Math.Clamp(top, 1, MaxTop);

        var query = origen.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            // El filtro de texto opera sobre las columnas comunes (el log no tiene detalles).
            var term = q.Trim();
            query = query.Where(t => t.Origen.Contains(term) || t.Destino.Contains(term));
        }
        if (lineaNegocio.HasValue)
            query = query.Where(t => t.LineaNegocio == lineaNegocio.Value);
        if (!string.IsNullOrWhiteSpace(tipoVehiculo))
            query = query.Where(t => t.TipoVehiculo == tipoVehiculo.Trim());
        if (!string.IsNullOrWhiteSpace(tipoCarroseria))
            query = query.Where(t => t.TipoCarroseria == tipoCarroseria.Trim());
        if (claseViaje.HasValue)
            query = query.Where(t => t.ClaseViaje == claseViaje.Value);

        var total = await query.CountAsync(ct);
        var filas = await query
            .OrderByDescending(t => t.Vigencia).ThenByDescending(t => t.FechaCreacion)
            .Take(top)
            .ToListAsync(ct);

        var nombres = await NombresDaneAsync(ct);
        var catalogos = await CatalogosAsync(ct);
        var items = filas.Select(t =>
        {
            // Detalles, flags y fecha de modificación solo existen en la tabla de plaza.
            var plaza = t as SapTarifaPlaza;
            return new SapTarifaHistoricoItemDto(
                t.Id,
                t.LineaNegocio, catalogos.Lineas.GetValueOrDefault(t.LineaNegocio),
                t.Origen, nombres.GetValueOrDefault(t.Origen),
                t.Destino, nombres.GetValueOrDefault(t.Destino),
                ParseValor(t.Valor), t.Valor, t.Vigencia,
                t.ClaseContenedor, Limpiar(t.TamanoContenedor), t.RangoPesoInf, t.RangoPesoSup,
                Limpiar(t.TipoVehiculo), NombreVehiculo(catalogos, t.TipoVehiculo),
                Limpiar(t.TipoCarroseria), Limpiar(t.TipoCarroseria) == null ? null : catalogos.Carrocerias.GetValueOrDefault(t.TipoCarroseria.Trim()),
                Limpiar(t.PesoLimite),
                t.ClaseViaje, catalogos.ClasesViaje.GetValueOrDefault(t.ClaseViaje),
                t.Devolucion, plaza?.Consolidado, plaza?.Exclusivo,
                Limpiar(plaza?.DetallesTarifa), t.FechaCreacion, plaza?.FechaModificacion ?? t.FechaCreacion,
                ajustesVigencia.TryGetValue(t.Id, out var vaj) ? vaj : null);
        }).ToList();

        return new SapHistoricoResultDto(total, items);
    }

    private async Task<SapHistoricoFiltrosDto> FiltrosCoreAsync<T>(
        IQueryable<T> origen, string cacheKey, CancellationToken ct) where T : SapTarifaBase
    {
        var filtros = await cache.GetOrCreateAsync(cacheKey, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var catalogos = await CatalogosAsync(ct);

            // Solo los valores realmente usados en la tabla (~10-25 K filas: barato).
            var lineas = await origen.Select(t => t.LineaNegocio).Distinct().OrderBy(x => x).ToListAsync(ct);
            var vehiculos = await origen.Select(t => t.TipoVehiculo).Distinct().ToListAsync(ct);
            var carrocerias = await origen.Select(t => t.TipoCarroseria).Distinct().ToListAsync(ct);
            var clasesViaje = await origen.Select(t => t.ClaseViaje).Distinct().OrderBy(x => x).ToListAsync(ct);

            return new SapHistoricoFiltrosDto(
                lineas.Select(id => new OpcionFiltroDto(id.ToString(), catalogos.Lineas.GetValueOrDefault(id) ?? $"Línea {id}")).ToList(),
                vehiculos.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct()
                    .Select(v => new OpcionFiltroDto(v, NombreVehiculo(catalogos, v) ?? $"Vehículo {v}"))
                    .OrderBy(o => o.Etiqueta).ToList(),
                carrocerias.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct()
                    .Select(c => new OpcionFiltroDto(c, catalogos.Carrocerias.GetValueOrDefault(c) ?? $"Carrocería {c}"))
                    .OrderBy(o => o.Etiqueta).ToList(),
                clasesViaje.Select(id => new OpcionFiltroDto(id.ToString(), catalogos.ClasesViaje.GetValueOrDefault(id) ?? $"Clase {id}")).ToList());
        });
        return filtros!;
    }

    /// <summary>
    /// Catálogo DANE (tblsapdane, ~10,7 K filas) como diccionario código → nombre.
    /// Es un catálogo estable: se carga una vez y se cachea 1 hora.
    /// </summary>
    private async Task<Dictionary<string, string>> NombresDaneAsync(CancellationToken ct)
    {
        var dict = await cache.GetOrCreateAsync("saphist:dane", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var centros = await db.CentrosPobladosDane
                .Select(d => new { d.CodigoCentroPoblado, d.NombreMostrar, d.NombreCentroPoblado, d.NombreDepartamento })
                .ToListAsync(ct);

            var map = new Dictionary<string, string>(centros.Count);
            foreach (var c in centros)
            {
                // NOMBREMOSTRAR viene vacío en parte del catálogo: se compone un nombre legible.
                var nombre = !string.IsNullOrWhiteSpace(c.NombreMostrar)
                    ? c.NombreMostrar.Trim()
                    : $"{c.NombreCentroPoblado.Trim()} ({c.NombreDepartamento.Trim()})";
                map.TryAdd(c.CodigoCentroPoblado.Trim(), nombre);
            }
            return map;
        });
        return dict!;
    }

    /// <summary>Catálogos SAP como diccionarios en memoria (estables: cache 1 hora).</summary>
    /// <remarks>
    /// Llaves de cruce verificadas contra los datos: LINEANEGOCIO y TIPOVEHICULO
    /// referencian el ID de su catálogo; TIPOCARROSERIA referencia el CODIGO.
    /// </remarks>
    private async Task<CatalogosSap> CatalogosAsync(CancellationToken ct)
    {
        var cat = await cache.GetOrCreateAsync("saphist:catalogos", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
            var lineas = await db.LineasNegocio.ToDictionaryAsync(l => l.Id, l => l.Descripcion.Trim(), ct);
            var vehiculos = await db.TiposVehiculo.ToDictionaryAsync(v => v.Id, v => $"{v.Codigo.Trim()} · {v.Descripcion.Trim()}", ct);
            var carrocerias = new Dictionary<string, string>();
            foreach (var c in await db.TiposCarroseria.ToListAsync(ct))
                carrocerias.TryAdd(c.Codigo.Trim(), c.Descripcion.Trim());
            var clasesViaje = await db.ClasesViaje.ToDictionaryAsync(c => c.Id, c => c.Descripcion.Trim(), ct);
            return new CatalogosSap(lineas, vehiculos, carrocerias, clasesViaje);
        });
        return cat!;
    }

    /// <summary>TIPOVEHICULO llega como texto ("1", "6", "") y referencia el ID del catálogo.</summary>
    private static string? NombreVehiculo(CatalogosSap catalogos, string? tipoVehiculo) =>
        int.TryParse(tipoVehiculo, out var id) ? catalogos.Vehiculos.GetValueOrDefault(id) : null;

    private sealed record CatalogosSap(
        Dictionary<int, string> Lineas,
        Dictionary<int, string> Vehiculos,
        Dictionary<string, string> Carrocerias,
        Dictionary<int, string> ClasesViaje);

    /// <summary>VALOR llega como texto desde SAP; null si no es un número válido.</summary>
    private static decimal? ParseValor(string valor) => ImporteParser.Parse(valor);

    /// <summary>SAP envía cadenas vacías en vez de null en varios campos.</summary>
    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
