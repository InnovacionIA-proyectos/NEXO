using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Infrastructure.External;
using Nexo.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Réplica de la lógica de negocio del tarifario SAP (getConsultarTarifaFlete,
/// ver docs getConsultarTarifaFlete_LogicaNegocio.md), portada 1:1 desde
/// Nexo.Api/Services/TarifarioSapService.cs del proyecto original:
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
/// beneficio real.
/// </summary>
public sealed class clsTarifarioSapAdapter : ITarifarioSapPort
{
    private const int MaxFilasBusqueda = 500;
    private const int IdVehiculoC2 = 4;
    private const decimal PesoMinimoSicetacC2 = 4301m;
    private const string CodigoCarroceriaFurgon = "4";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    // internal (no private): S5-HU-19.1/#281 las reutiliza para resolver el NOMBRE del
    // catálogo externo (ILineasNegocioPort/IClasesViajePort) que necesita el matching
    // exacto de clsTarifaPlazaInternaAdapter — un único mapeo enum->Id SAP, no dos.
    internal static readonly Dictionary<LineaNegocio, int> CodigoLinea = new()
    {
        [LineaNegocio.PuertoAInterior] = 1,
        [LineaNegocio.InteriorAPuerto] = 2,
        [LineaNegocio.Nacional] = 4,
        [LineaNegocio.DevolucionContenedor] = 6,
        [LineaNegocio.Andino] = 7,
        [LineaNegocio.Licitaciones] = 8,
    };

    internal static readonly Dictionary<ClaseViaje, int> CodigoClaseViaje = new()
    {
        [ClaseViaje.Contenedor] = 1,
        [ClaseViaje.Cupo] = 2,
        [ClaseViaje.CargaSuelta] = 4,
        [ClaseViaje.Extradimensionado] = 5,
    };

    // S5-HU-19.1/#281: "PTO" por "PUERTO" (ej. zona SAP "ZC_PTO_BOLIVAR" vs ruta
    // "Puerto Bolívar") — el único caso concreto que trajo el issue. Lista corta y
    // explícita a propósito: cada abreviatura nueva se agrega acá cuando aparezca un
    // caso real, no se adivina un diccionario completo sin evidencia.
    private static readonly (string Abreviatura, string Completo)[] Abreviaturas = [("PTO", "PUERTO")];

    private readonly clsDbSolucionesTiDbContext? _dc;
    private readonly clsDwSiceDbContext? _dw;
    private readonly clsNexoDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IAjustesVigenciaPlazaRepository _ajustes;

    public clsTarifarioSapAdapter(
        IServiceProvider sp, clsNexoDbContext db, IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes)
    {
        _dc = sp.GetService<clsDbSolucionesTiDbContext>();
        _dw = sp.GetService<clsDwSiceDbContext>();
        _db = db;
        _cache = cache;
        _clock = clock;
        _ajustes = ajustes;
    }

    public async Task<clsResultadoTarifarioSap?> ConsultarAsync(clsConsultaTarifarioSap c, CancellationToken ct = default)
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

        if (candidatas.Count == 0)
            return new clsResultadoTarifarioSap(null, Vencida: false, sicetac);

        var ajustes = await _ajustes.MapaAsync(ct);
        if (ajustes.Count > 0)
            foreach (var t in candidatas)
                if (ajustes.TryGetValue(t.Id, out var vigenciaAjustada))
                    t.Vigencia = vigenciaAjustada;

        candidatas = candidatas.OrderByDescending(t => t.Vigencia).ToList();
        var hoy = _clock.GetUtcNow().Date;
        var (tarifa, vencida) = Seleccionar(c, candidatas, hoy, lineaId, claseViajeId, claseContId, vehiculoId, carroceriaCod, criterio);
        return new clsResultadoTarifarioSap(tarifa, vencida, sicetac);
    }

    private async Task<(List<clsSapTarifaPlaza> Candidatas, string Criterio)> BuscarConZonasAsync(
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

        // S5-HU-19.1/#281: antes solo "z.Transporte.Contains(origen)" — fallaba contra
        // códigos de zona que traen una ABREVIATURA ("ZC_PTO_BOLIVAR" vs "Puerto Bolívar")
        // o que no traen el nombre de la ciudad en absoluto (p. ej. "VABURRA" = Valle de
        // Aburrá = Medellín). clsResolucionZonaSap (que resolvía esto con un diccionario de
        // alias manual) se retiró en S5-HU-16.1/#254 a favor de tbl_municipios.NombreTarifasPlaza/
        // ZonaTarificacion, ya sembrados con el código de zona correcto por municipio
        // (S4-HU-09.1) — se reutiliza ese mismo mecanismo acá en vez de resucitar el
        // diccionario de alias manual. La comparación directa ahora prueba las DOS
        // direcciones de "contiene" (antes solo una) y expande abreviaturas conocidas.
        bool Coincide(string zonaTransporte, string texto)
        {
            var expandida = ExpandirAbreviaturas(zonaTransporte);
            return expandida.Contains(texto) || texto.Contains(expandida);
        }

        var zonasOrigen = zonas.Where(z => Coincide(z.Transporte, origen)).Select(z => z.Zona).Distinct().ToList();
        var zonasDestino = zonas.Where(z => Coincide(z.Transporte, destino)).Select(z => z.Zona).Distinct().ToList();

        if (zonasOrigen.Count == 0) zonasOrigen = await ZonasPorMunicipioAsync(origen, ct);
        if (zonasDestino.Count == 0) zonasDestino = await ZonasPorMunicipioAsync(destino, ct);

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

    private static (clsCandidatoTarifaDto? Tarifa, bool Vencida) Seleccionar(
        clsConsultaTarifarioSap c, List<clsSapTarifaPlaza> candidatas, DateTime hoy,
        int? lineaId, int? claseViajeId, int? claseContId, int? vehiculoId, string? carroceriaCod, string criterio)
    {
        var vehiculoIdStr = vehiculoId?.ToString();

        bool PesoEnRango(clsSapTarifaPlaza t) =>
            c.Peso is not { } p
            || (t.RangoPesoInf is null && t.RangoPesoSup is null)
            || (t.RangoPesoInf <= p && p <= t.RangoPesoSup);

        bool ContenedorCoincide(clsSapTarifaPlaza t) => claseContId is null || t.ClaseContenedor == claseContId;

        bool CupoCoincide(clsSapTarifaPlaza t) =>
            (vehiculoIdStr is null || t.TipoVehiculo.Trim() == vehiculoIdStr) &&
            (carroceriaCod is null || t.TipoCarroseria.Trim() == carroceriaCod);

        clsCandidatoTarifaDto? Candidato(clsSapTarifaPlaza t, string regla, string? advertencia = null)
        {
            var valor = ParseValor(t.Valor);
            if (valor is not > 0) return null;
            return new clsCandidatoTarifaDto(
                "plaza", valor.Value,
                $"{t.Origen.Trim()} → {t.Destino.Trim()} · vigencia {t.Vigencia:yyyy-MM-dd}" +
                    (t.DetallesTarifa is { Length: > 0 } det ? $" · {det}" : ""),
                $"{regla} · {criterio}", candidatas.Count, advertencia);
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
        if (fallback.Fila is null) return (null, false);

        // Verificado con datos reales (control A/B) que este fallback proponía "la
        // tarifa vigente de mayor valor" en silencio incluso cuando el peso de la
        // línea excedía el límite de TODAS las tarifas para ese vehículo, o cuando el
        // vehículo no resolvió en el catálogo SAP - sin distinguir esos casos de un
        // match exacto real. La línea sigue cotizándose con este candidato (no se
        // descarta), pero ahora con una advertencia explícita para el Administrador
        // de Fletes/cliente antes de confirmar.
        string advertencia;
        if (claseViajeId == 2 && vehiculoIdStr is null)
        {
            advertencia = "No se pudo identificar el tipo de vehículo de esta línea en el catálogo de tarifas " +
                "SAP: se propone la tarifa de plaza vigente de mayor valor para la ruta como referencia, sin " +
                "garantía de que corresponda al vehículo real. Verifique manualmente antes de confirmar.";
        }
        else if (claseViajeId == 2 && vehiculoIdStr is not null && c.Peso is { } pesoConsulta &&
                 !candidatas.Any(x => x.Vigencia >= hoy && CupoCoincide(x) && ParsePeso(x.PesoLimite) is { } limDisponible && pesoConsulta <= limDisponible))
        {
            advertencia = $"El peso de esta línea ({pesoConsulta:N0} kg) excede el límite de todas las tarifas " +
                "de plaza vigentes para este vehículo en la ruta: se propone la tarifa vigente de mayor valor " +
                "como referencia, sin garantía de que cubra este peso. Verifique manualmente antes de confirmar.";
        }
        else
        {
            advertencia = "No se encontró una tarifa de plaza que calce exactamente con las condiciones de " +
                "esta línea (vehículo, peso o tipo de viaje): se propone la tarifa vigente de mayor valor de " +
                "la ruta como referencia. Verifique manualmente antes de confirmar.";
        }

        return (Candidato(fallback.Fila, "fallback · mayor valor vigente", advertencia), false);
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

    private async Task<List<clsSapTarifaPlaza>> BuscarAsync(
        int? lineaId, int? claseViajeId, int? claseContId, int? vehiculoId, string? carroceriaCod, decimal? peso,
        System.Linq.Expressions.Expression<Func<clsSapTarifaPlaza, bool>> filtroOrigen,
        System.Linq.Expressions.Expression<Func<clsSapTarifaPlaza, bool>> filtroDestino,
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

    private async Task<clsCandidatoTarifaDto?> SicetacOficialAsync(
        clsConsultaTarifarioSap c, string origen, string destino,
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

        // #300: solo el período del mes y año actuales (sin mes de gracia). Se busca el más reciente HASTA el
        // actual: un período futuro ya cargado no tapa al vigente. Si no es el actual (o no hay ninguno) no hay piso.
        var ahora = _clock.GetUtcNow();
        var actual = clsPeriodoSicetac.Actual(ahora);
        var periodo = await _cache.GetOrCreateAsync($"tarifario:sice-periodo:{actual}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await _dw.RutasSicetac.Where(r => r.Periodo > 0 && r.Periodo <= actual).MaxAsync(r => (int?)r.Periodo, ct) ?? 0;
        });
        if (!clsPeriodoSicetac.EsElDelMesActual(periodo, ahora)) return null;

        // La ruta se compara exacta por código DANE cuando la línea trae los dos municipios resueltos: tblRutasSicetac guarda el
        // mismo código pero SIN el cero a la izquierda del departamento (tbl_municipios "05001000" ~ DWSice "5001000"). El
        // texto del nombre de la ruta es solo el respaldo (línea sin municipios): como "%YUMBO%-%CALI%" también coincide con
        // "YUMBO - CALIMA" y se toma el valor más alto, elegía la ciudad equivocada.
        var porDane = c.OrigenDane is not null && c.DestinoDane is not null;
        var origenSinCeros = c.OrigenDane?.TrimStart('0');
        var destinoSinCeros = c.DestinoDane?.TrimStart('0');
        var rutas = _dw.RutasSicetac
            .Where(r => r.Periodo == periodo &&
                        r.CondicionCarga == "CARGADO" &&
                        r.NombreTipoCarga == tipoCarga &&
                        r.NombreUnidadTransporte == unidad &&
                        r.Configuracion == config &&
                        r.ViaEstandar == "SI");
        rutas = porDane
            ? rutas.Where(r => r.Origen == origenSinCeros && r.Destino == destinoSinCeros)
            : rutas.Where(r => EF.Functions.Like(r.NombreRuta, $"%{origen}%-%{destino}%"));
        var top = await rutas.OrderByDescending(r => r.ValorSiceConHora).FirstOrDefaultAsync(ct);
        if (top is null) return null;

        // S4-HU-14.1/#221: bug gemelo del fallback tolerante de PropuestaTarifaAdapter —
        // este camino "oficial" también tomaba ValorSiceConHora tal cual, asumiendo las
        // horas pactadas. Mismo helper compartido (clsPisoConHoras), mismo criterio de
        // fallback: >0 se trata igual que antes, <=0 o null cae a top.Valor.
        var valorSiceConHora = top.ValorSiceConHora is > 0 ? top.ValorSiceConHora : null;
        var valor = clsPisoConHoras.Calcular(top.Valor, top.ValorHora, top.HorasPactadas, valorSiceConHora, c.HorasSolicitud);
        return new clsCandidatoTarifaDto(
            "sicetac", valor,
            $"{top.NombreRuta} · {top.Configuracion} · {top.NombreTipoCarga} · {top.NombreUnidadTransporte} · vía estándar",
            $"oficial · periodo {periodo} · CARGADO · carga '{tipoCarga}' · unidad '{unidad}' · configuración {config}" +
                (porDane ? $" · ruta DANE {c.OrigenDane}-{c.DestinoDane}" : string.Empty), 1,
            ValorPisoBase: top.Valor, ValorTiemposLogisticos: valor - top.Valor, Configuracion: top.Configuracion);
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

    private static string ExpandirAbreviaturas(string zona)
    {
        foreach (var (abreviatura, completo) in Abreviaturas)
            if (zona.Contains(abreviatura)) return zona.Replace(abreviatura, completo);
        return zona;
    }

    /// <summary>
    /// S5-HU-19.1/#281: antes de rendirse, busca el municipio por nombre (misma regla de
    /// contención en las 2 direcciones que <see cref="CodigosDaneAsync"/>) y devuelve sus
    /// códigos de zona YA sembrados (<c>NombreTarifasPlaza</c>/<c>ZonaTarificacion</c>,
    /// S4-HU-09.1) — el reemplazo de <c>clsResolucionZonaSap.AliasZonasAgregadas</c>
    /// (retirado en S5-HU-16.1/#254): en vez de un diccionario de alias mantenido a mano
    /// ("Medellín" -> "VABURRA"), el código de zona ya vive en la fila del municipio.
    /// </summary>
    private async Task<List<string>> ZonasPorMunicipioAsync(string textoNormalizado, CancellationToken ct)
    {
        var municipios = await _cache.GetOrCreateAsync("tarifario:municipios-zonas", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await _db.Municipios.AsNoTracking()
                .Where(m => m.NombreTarifasPlaza != null || m.ZonaTarificacion != null)
                .Select(m => new { m.Municipio, m.NombreTarifasPlaza, m.ZonaTarificacion })
                .ToListAsync(ct);
        });

        // Exacto primero, contención como último recurso — mismo motivo que
        // clsTarifaPlazaInternaAdapter.ResolverMunicipiosAsync: nombres cortos o
        // compuestos ("Buenaventura" contiene a "Ventura", Cañar/Ecuador) cruzan con
        // municipios de otro país si solo se compara por contención.
        var exactos = municipios!.Where(m => Normalizar(m.Municipio) == textoNormalizado).ToList();
        var candidatos = exactos.Count > 0
            ? exactos
            : municipios!.Where(m => { var n = Normalizar(m.Municipio); return n.Length > 0 && (textoNormalizado.Contains(n) || n.Contains(textoNormalizado)); }).ToList();

        return candidatos
            .SelectMany(m => new[] { m.NombreTarifasPlaza, m.ZonaTarificacion })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToList();
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

    private static decimal? ParseValor(string valor) => clsImporteParser.Parse(valor);

    private static decimal? ParsePeso(string? pesoLimite) => clsImporteParser.Parse(pesoLimite);

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
