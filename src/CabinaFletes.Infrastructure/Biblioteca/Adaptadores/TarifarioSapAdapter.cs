using System.Globalization;
using System.Text;
using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using CabinaFletes.Application.CasosDeUso.Biblioteca.DTOs;
using CabinaFletes.Application.CasosDeUso.Biblioteca.Interfaces;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Réplica de la lógica de negocio del tarifario SAP (getConsultarTarifaFlete,
/// ver docs getConsultarTarifaFlete_LogicaNegocio.md), portada 1:1 desde
/// CabinaFletes.Api/Services/TarifarioSapService.cs del proyecto original:
///
/// 1. Búsqueda DIRECTA en tblSAPTarifas por zona de transporte / DIVIPOLA, con
///    filtros de línea de negocio, clase de viaje y (para contenedor) rango de
///    peso + clase de contenedor, o (para cupo) vehículo + carrocería.
/// 2. Si no hay resultados, fallback por ZONAS DE TARIFICACIÓN.
/// 3. Selección: Regla A (devolución línea 6), Regla B (contenedor), Regla C
///    (cupo). Las vencidas se devuelven marcadas ("inactiva"), nunca se proponen.
///    Sin regla aplicable, fallback conservador: mayor valor vigente.
/// 4. SICETAC oficial: solo si la condición de comparación del documento aplica.
///
/// Este adapter concentra deliberadamente reglas de negocio junto con las
/// consultas EF: la lógica está indisolublemente ligada a la forma de las
/// tablas SAP/DW (IQueryable dinámico según catálogos resueltos en runtime).
/// Separarla en Application obligaría a exponer un "mini-ORM" de puertos sin
/// beneficio real — ver MIGRACION.md, sección Biblioteca.
/// </summary>
public sealed class TarifarioSapAdapter : ITarifarioSapPort
{
    private const int MaxFilasBusqueda = 500;
    private const int IdVehiculoC2 = 4;
    private const decimal PesoMinimoSicetacC2 = 4301m;
    private const string CodigoCarroceriaFurgon = "4";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private static readonly Dictionary<LineaNegocio, int> CodigoLinea = new()
    {
        [LineaNegocio.PuertoAInterior] = 1,
        [LineaNegocio.InteriorAPuerto] = 2,
        [LineaNegocio.Nacional] = 4,
        [LineaNegocio.DevolucionContenedor] = 6,
        [LineaNegocio.Andino] = 7,
        [LineaNegocio.Licitaciones] = 8,
    };

    private static readonly Dictionary<ClaseViaje, int> CodigoClaseViaje = new()
    {
        [ClaseViaje.Contenedor] = 1,
        [ClaseViaje.Cupo] = 2,
        [ClaseViaje.CargaSuelta] = 4,
        [ClaseViaje.Extradimensionado] = 5,
    };

    private readonly DbSolucionesTiDbContext? _dc;
    private readonly DwSiceDbContext? _dw;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IAjustesVigenciaPlazaRepository _ajustes;

    public TarifarioSapAdapter(IServiceProvider sp, IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes)
    {
        _dc = sp.GetService<DbSolucionesTiDbContext>();
        _dw = sp.GetService<DwSiceDbContext>();
        _cache = cache;
        _clock = clock;
        _ajustes = ajustes;
    }

    public async Task<ResultadoTarifarioSap?> ConsultarAsync(ConsultaTarifarioSap c, CancellationToken ct = default)
    {
        if (_dc is null) return null;

        var origen = Normalizar(c.Origen);
        var destino = Normalizar(c.Destino);
        var lineaId = c.LineaNegocio is { } ln ? CodigoLinea[ln] : (int?)null;
        var claseViajeId = c.ClaseViaje is { } cv ? CodigoClaseViaje[cv] : (int?)null;
        var claseContId = await IdClaseContenedorAsync(c.ClaseContenedor, ct);
        var vehiculoId = await IdVehiculoAsync(c.ConfigVehiculo, ct);
        var carroceriaCod = await CodigoCarroceriaAsync(c.Carroceria, ct);

        var (candidatas, criterio) = await BuscarConZonasAsync(
            lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, c.Peso, origen, destino, ct);

        var sicetac = await SicetacOficialAsync(c, origen, destino, claseContId, vehiculoId, carroceriaCod, ct);

        var retorno = c.RetornaContenedor && !string.IsNullOrWhiteSpace(c.CiudadDevolucion)
            ? await RetornoAsync(claseViajeId, claseContId, destino, Normalizar(c.CiudadDevolucion), ct)
            : null;

        if (candidatas.Count == 0)
            return new ResultadoTarifarioSap(null, Vencida: false, sicetac, retorno);

        var ajustes = await _ajustes.MapaAsync(ct);
        if (ajustes.Count > 0)
            foreach (var t in candidatas)
                if (ajustes.TryGetValue(t.Id, out var vigenciaAjustada))
                    t.Vigencia = vigenciaAjustada;

        candidatas = candidatas.OrderByDescending(t => t.Vigencia).ToList();
        var hoy = _clock.GetUtcNow().Date;
        var (tarifa, vencida) = Seleccionar(c, candidatas, hoy, lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, criterio);
        return new ResultadoTarifarioSap(tarifa, vencida, sicetac, retorno);
    }

    private async Task<(List<SapTarifaPlaza> Candidatas, string Criterio)> BuscarConZonasAsync(
        int? lineaId, int? claseViajeId, int? claseContId, int? vehiculoId, string? carroceriaCod, decimal? peso,
        string origen, string destino, CancellationToken ct)
    {
        var codigosOrigen = await CodigosDaneAsync(origen, ct);
        var codigosDestino = await CodigosDaneAsync(destino, ct);

        var candidatas = await BuscarAsync(
            lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, peso,
            t => codigosOrigen.Contains(t.Origen) || EF.Functions.Like(t.Origen, $"%{origen}%"),
            t => codigosDestino.Contains(t.Destino) || EF.Functions.Like(t.Destino, $"%{destino}%"),
            ct);
        var criterio = $"directa · origen ~ '{origen}' · destino ~ '{destino}'";
        if (candidatas.Count > 0) return (candidatas, criterio);

        var zonas = await ZonasTarificacionAsync(ct);
        var zonasOrigen = zonas.Where(z => z.Transporte.Contains(origen)).Select(z => z.Zona).Distinct().ToList();
        var zonasDestino = zonas.Where(z => z.Transporte.Contains(destino)).Select(z => z.Zona).Distinct().ToList();

        if (zonasOrigen.Count > 0 && zonasDestino.Count == 0)
        {
            foreach (var o in zonasOrigen)
                candidatas.AddRange(await BuscarAsync(
                    lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, peso,
                    t => t.Origen == o,
                    t => codigosDestino.Contains(t.Destino) || EF.Functions.Like(t.Destino, $"%{destino}%"),
                    ct));
            criterio = $"zona origen ({string.Join(",", zonasOrigen)}) · destino ~ '{destino}'";
        }
        else if (zonasOrigen.Count == 0 && zonasDestino.Count > 0)
        {
            foreach (var d in zonasDestino)
                candidatas.AddRange(await BuscarAsync(
                    lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, peso,
                    t => codigosOrigen.Contains(t.Origen) || EF.Functions.Like(t.Origen, $"%{origen}%"),
                    t => t.Destino == d,
                    ct));
            criterio = $"origen ~ '{origen}' · zona destino ({string.Join(",", zonasDestino)})";
        }
        else if (zonasOrigen.Count > 0 && zonasDestino.Count > 0)
        {
            foreach (var o in zonasOrigen)
                foreach (var d in zonasDestino)
                    candidatas.AddRange(await BuscarAsync(
                        lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, peso,
                        t => t.Origen == o, t => t.Destino == d, ct));
            criterio = $"zonas ({string.Join(",", zonasOrigen)}) → ({string.Join(",", zonasDestino)})";
        }

        return (candidatas.GroupBy(t => t.Id).Select(g => g.First()).ToList(), criterio);
    }

    private async Task<CandidatoTarifaDto?> RetornoAsync(
        int? claseViajeId, int? claseContId, string origenRetorno, string destinoRetorno, CancellationToken ct)
    {
        var (candidatas, criterio) = await BuscarConZonasAsync(
            lineaId: 6, claseViajeId, claseContId, vehiculoId: null, carroceriaCod: null, peso: null,
            origenRetorno, destinoRetorno, ct);

        var hoy = _clock.GetUtcNow().Date;
        var top = candidatas
            .Where(t => t.Exclusivo == true && t.Vigencia > hoy &&
                        (claseContId is null || t.ClaseContenedor == claseContId))
            .OrderByDescending(t => t.Vigencia)
            .Select(t => (Fila: t, Valor: ParseValor(t.Valor)))
            .FirstOrDefault(x => x.Valor is > 0);
        if (top.Fila is null) return null;

        return new CandidatoTarifaDto(
            "retorno_contenedor", top.Valor!.Value,
            $"{top.Fila.Origen.Trim()} → {top.Fila.Destino.Trim()} · vigencia {top.Fila.Vigencia:yyyy-MM-dd}" +
                (top.Fila.DetallesTarifa is { Length: > 0 } det ? $" · {det}" : ""),
            $"retorno · línea 6 exclusivo · {criterio}", candidatas.Count);
    }

    private static (CandidatoTarifaDto? Tarifa, bool Vencida) Seleccionar(
        ConsultaTarifarioSap c, List<SapTarifaPlaza> candidatas, DateTime hoy,
        int? lineaId, int? claseViajeId, int? claseContId, int? vehiculoId, string? carroceriaCod, string criterio)
    {
        var vehiculoIdStr = vehiculoId?.ToString();

        bool PesoEnRango(SapTarifaPlaza t) =>
            c.Peso is not { } p
            || (t.RangoPesoInf is null && t.RangoPesoSup is null)
            || (t.RangoPesoInf <= p && p <= t.RangoPesoSup);

        bool ContenedorCoincide(SapTarifaPlaza t) => claseContId is null || t.ClaseContenedor == claseContId;

        bool CupoCoincide(SapTarifaPlaza t) =>
            (vehiculoIdStr is null || t.TipoVehiculo.Trim() == vehiculoIdStr) &&
            (carroceriaCod is null || t.TipoCarroseria.Trim() == carroceriaCod);

        CandidatoTarifaDto? Candidato(SapTarifaPlaza t, string regla)
        {
            var valor = ParseValor(t.Valor);
            if (valor is not > 0) return null;
            return new CandidatoTarifaDto(
                "plaza", valor.Value,
                $"{t.Origen.Trim()} → {t.Destino.Trim()} · vigencia {t.Vigencia:yyyy-MM-dd}" +
                    (t.DetallesTarifa is { Length: > 0 } det ? $" · {det}" : ""),
                $"{regla} · {criterio}", candidatas.Count);
        }

        foreach (var t in candidatas)
        {
            if (t.Vigencia >= hoy)
            {
                if (claseViajeId == 1 && lineaId == 6 && ContenedorCoincide(t))
                {
                    if (c.Consolidado && t.Consolidado == true)
                        return (Candidato(t, "Regla A devolución · consolidado"), false);
                    if (c.Exclusivo && t.Exclusivo == true)
                        return (Candidato(t, "Regla A devolución · exclusivo"), false);
                }

                if (claseViajeId == 1 && lineaId != 4 && lineaId != 6)
                {
                    var match = candidatas.FirstOrDefault(x =>
                        x.Vigencia >= hoy && PesoEnRango(x) && ContenedorCoincide(x) && ParseValor(x.Valor) is > 0);
                    if (match is not null)
                        return (Candidato(match, "Regla B contenedor · peso en rango"), false);
                }

                if (claseViajeId == 2 && vehiculoIdStr is not null && CupoCoincide(t) &&
                    c.Peso is { } peso && ParsePeso(t.PesoLimite) is { } lim && peso <= lim)
                {
                    var pesos = candidatas
                        .Where(x => CupoCoincide(x) && ParsePeso(x.PesoLimite) is not null)
                        .Select(x => ParsePeso(x.PesoLimite)!.Value)
                        .Distinct().OrderBy(x => x).ToList();
                    var elegido = ValorMasCercano(pesos, peso);
                    var match = candidatas.FirstOrDefault(x =>
                        x.Vigencia >= hoy && CupoCoincide(x) && ParsePeso(x.PesoLimite) == elegido && ParseValor(x.Valor) is > 0);
                    if (match is not null)
                        return (Candidato(match, $"Regla C cupo · peso límite {elegido:0} kg"), false);
                }
            }
            else
            {
                if (claseViajeId == 1 && lineaId != 4 && ContenedorCoincide(t) && (lineaId == 6 || PesoEnRango(t)))
                {
                    var aplica = lineaId != 6 || (c.Consolidado && t.Consolidado == true) || (c.Exclusivo && t.Exclusivo == true);
                    if (aplica)
                        return (Candidato(t, "TARIFA INACTIVA (vigencia vencida)"), true);
                }
                if (claseViajeId == 2 && vehiculoIdStr is not null && CupoCoincide(t) &&
                    (c.Peso is not { } p2 || (ParsePeso(t.PesoLimite) is { } l2 && p2 <= l2)))
                {
                    return (Candidato(t, "TARIFA INACTIVA (vigencia vencida)"), true);
                }
            }
        }

        var fallback = candidatas
            .Where(t => t.Vigencia >= hoy)
            .Select(t => (Fila: t, Valor: ParseValor(t.Valor)))
            .Where(x => x.Valor is > 0)
            .OrderByDescending(x => x.Valor)
            .FirstOrDefault();
        return fallback.Fila is null
            ? (null, false)
            : (Candidato(fallback.Fila, "fallback · mayor valor vigente"), false);
    }

    internal static decimal ValorMasCercano(IReadOnlyList<decimal> ordenados, decimal entrada)
    {
        if (ordenados.Count == 0) return entrada;
        var idx = 0;
        while (idx < ordenados.Count && ordenados[idx] < entrada) idx++;
        if (idx < ordenados.Count && ordenados[idx] == entrada) return entrada;
        if (idx == 0) return ordenados[0];
        if (idx >= ordenados.Count) return ordenados[^1];
        var inferior = ordenados[idx - 1];
        var superior = ordenados[idx];
        return entrada <= inferior ? inferior : superior;
    }

    private async Task<List<SapTarifaPlaza>> BuscarAsync(
        int? lineaId, int? claseViajeId, int? claseContId, int? vehiculoId, string? carroceriaCod, decimal? peso,
        System.Linq.Expressions.Expression<Func<SapTarifaPlaza, bool>> filtroOrigen,
        System.Linq.Expressions.Expression<Func<SapTarifaPlaza, bool>> filtroDestino,
        CancellationToken ct)
    {
        var query = _dc!.TarifasPlazaSap.Where(filtroOrigen).Where(filtroDestino);

        if (lineaId is { } li) query = query.Where(t => t.LineaNegocio == li);
        if (claseViajeId is { } cv) query = query.Where(t => t.ClaseViaje == cv);

        if (claseContId is { } cc)
        {
            query = query.Where(t => t.ClaseContenedor == cc);
            if (peso is { } p && lineaId != 6)
                query = query.Where(t => t.RangoPesoInf == null || (t.RangoPesoInf <= p && p <= t.RangoPesoSup));
        }

        if (claseViajeId == 2)
        {
            if (vehiculoId is { } v)
            {
                var vs = v.ToString();
                query = query.Where(t => t.TipoVehiculo == vs);
            }
            if (carroceriaCod is not null)
                query = query.Where(t => t.TipoCarroseria == carroceriaCod);
        }

        return await query.AsNoTracking().OrderByDescending(t => t.Vigencia).Take(MaxFilasBusqueda).ToListAsync(ct);
    }

    private async Task<CandidatoTarifaDto?> SicetacOficialAsync(
        ConsultaTarifarioSap c, string origen, string destino,
        int? claseContId, int? vehiculoId, string? carroceriaCod, CancellationToken ct)
    {
        if (_dw is null || c.ClaseViaje is null) return null;
        var claseViajeId = CodigoClaseViaje[c.ClaseViaje.Value];
        var lineaId = c.LineaNegocio is { } ln ? CodigoLinea[ln] : (int?)null;

        var aplica =
            (claseViajeId == 1 && lineaId != 6) ||
            (claseViajeId == 2 && (vehiculoId != IdVehiculoC2 || (c.Peso ?? 0) >= PesoMinimoSicetacC2));
        if (!aplica) return null;

        string? config;
        if (claseViajeId == 1)
            config = await ConfigPorRangoPesoAsync(claseContId, c.Peso, ct);
        else
            config = string.IsNullOrWhiteSpace(c.ConfigVehiculo)
                ? null
                : c.ConfigVehiculo.Trim().ToUpperInvariant().TrimStart('C');
        if (string.IsNullOrWhiteSpace(config)) return null;

        var tipoCarga = await ParametroAsync(457, ct) ?? "General";
        var unidad = config == "2L3" || carroceriaCod == CodigoCarroceriaFurgon
            ? "FURGON"
            : await ParametroAsync(458, ct) ?? "ESTACAS";

        var periodo = await _cache.GetOrCreateAsync("tarifario:sice-periodo", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await _dw.RutasSicetac.Where(r => r.Periodo > 0).MaxAsync(r => (int?)r.Periodo, ct) ?? 0;
        });
        if (periodo == 0) return null;

        var top = await _dw.RutasSicetac
            .Where(r => r.Periodo == periodo &&
                        r.CondicionCarga == "CARGADO" &&
                        r.NombreTipoCarga == tipoCarga &&
                        r.NombreUnidadTransporte == unidad &&
                        r.Configuracion == config &&
                        r.ViaEstandar == "SI" &&
                        EF.Functions.Like(r.NombreRuta, $"%{origen}%-%{destino}%"))
            .OrderByDescending(r => r.ValorSiceConHora)
            .FirstOrDefaultAsync(ct);
        if (top is null) return null;

        var valor = top.ValorSiceConHora is > 0 ? top.ValorSiceConHora.Value : top.Valor;
        return new CandidatoTarifaDto(
            "sicetac", valor,
            $"{top.NombreRuta} · {top.Configuracion} · {top.NombreTipoCarga} · {top.NombreUnidadTransporte} · vía estándar",
            $"oficial · periodo {periodo} · CARGADO · carga '{tipoCarga}' · unidad '{unidad}' · configuración {config}", 1);
    }

    private async Task<string?> ConfigPorRangoPesoAsync(int? claseContId, decimal? peso, CancellationToken ct)
    {
        if (claseContId is null || peso is null) return null;
        var rangos = await _cache.GetOrCreateAsync("tarifario:rangos-peso", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var rel = await _dc!.ContenedorRangosPeso.ToListAsync(ct);
            var rp = await _dc.RangosPeso.ToListAsync(ct);
            return rel.Join(rp, x => x.IdRangoPeso, r => r.Id,
                    (x, r) => (x.IdClaseContenedor, r.PesoMinimo, r.PesoMaximo, r.TipoVehiculo))
                .ToList();
        });
        return rangos!
            .Where(r => r.IdClaseContenedor == claseContId && r.PesoMinimo <= peso && peso <= r.PesoMaximo)
            .Select(r => r.TipoVehiculo.Trim())
            .FirstOrDefault();
    }

    private async Task<int?> IdClaseContenedorAsync(string? claseContenedor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(claseContenedor)) return null;
        var mapa = await _cache.GetOrCreateAsync("tarifario:clases-contenedor", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await _dc!.ClasesContenedor.ToListAsync(ct);
            return lista.ToDictionary(x => Normalizar(x.Descripcion), x => x.Id);
        });
        return mapa!.TryGetValue(Normalizar(claseContenedor), out var id) ? id : null;
    }

    private async Task<int?> IdVehiculoAsync(string? config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config)) return null;
        var mapa = await _cache.GetOrCreateAsync("tarifario:vehiculos", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await _dc!.TiposVehiculo.Select(v => new { v.Id, v.Codigo }).ToListAsync(ct);
            return lista
                .GroupBy(v => v.Codigo.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);
        });
        var codigo = config.Trim().ToUpperInvariant();
        if (mapa!.TryGetValue(codigo, out var id)) return id;
        return mapa.TryGetValue("C" + codigo, out var idC) ? idC : null;
    }

    private async Task<string?> CodigoCarroceriaAsync(string? carroceria, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(carroceria)) return null;
        var mapa = await _cache.GetOrCreateAsync("tarifario:carrocerias", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await _dc!.TiposCarroseria.ToListAsync(ct);
            return lista
                .GroupBy(x => Normalizar(x.Descripcion))
                .ToDictionary(g => g.Key, g => g.First().Codigo.Trim());
        });
        var buscado = Normalizar(carroceria);
        return mapa!.TryGetValue(buscado, out var cod)
            ? cod
            : mapa.FirstOrDefault(kv => buscado.Contains(kv.Key)).Value;
    }

    private async Task<string?> ParametroAsync(int idParametro, CancellationToken ct)
    {
        var valor = await _cache.GetOrCreateAsync($"tarifario:parametro-{idParametro}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _dc!.Parametros
                .Where(p => p.IdParametro == idParametro)
                .Select(p => p.ValorParametro)
                .FirstOrDefaultAsync(ct);
        });
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    private async Task<List<(string Zona, string Transporte)>> ZonasTarificacionAsync(CancellationToken ct)
    {
        var zonas = await _cache.GetOrCreateAsync("tarifario:zonas-tarificacion", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await _dc!.ZonasTarificacion.ToListAsync(ct);
            return lista
                .Select(z => (Zona: z.IdZonaTarificacion.Trim(), Transporte: Normalizar(z.IdZonaTransporte)))
                .ToList();
        });
        return zonas!;
    }

    private async Task<List<string>> CodigosDaneAsync(string ciudad, CancellationToken ct)
    {
        var municipios = await _cache.GetOrCreateAsync("prop:dane-municipios", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var todos = await _dc!.CentrosPobladosDane
                .Select(d => new { d.CodigoCentroPoblado, d.NombreMunicipio })
                .ToListAsync(ct);
            return todos.Select(d => (Codigo: d.CodigoCentroPoblado.Trim(), Nombre: Normalizar(d.NombreMunicipio))).ToList();
        });
        return municipios!
            .Where(m => m.Nombre.Contains(ciudad) || ciudad.Contains(m.Nombre))
            .Select(m => m.Codigo)
            .Distinct()
            .ToList();
    }

    private static decimal? ParseValor(string valor) => ImporteParser.Parse(valor);

    private static decimal? ParsePeso(string? pesoLimite) => ImporteParser.Parse(pesoLimite);

    private static string Normalizar(string s)
    {
        var limpio = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var d = limpio.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
