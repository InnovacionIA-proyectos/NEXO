using System.Globalization;
using System.Text;
using CabinaFletes.Application.AjustesVigenciaPlaza.Ports;
using CabinaFletes.Application.Biblioteca.Dtos;
using CabinaFletes.Application.Biblioteca.Ports;
using CabinaFletes.Application.Common.Dtos;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Application.ReglasSoloSicetac.Ports;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Common;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Infrastructure.External;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Infrastructure.Adapters;

/// <summary>
/// Motor de propuesta de tarifa: por cada línea de la solicitud consulta las tres
/// fuentes de la Biblioteca —piso SICE-TAC (DWSice), tarifas de plaza SAP e
/// histórico SAP— y propone el valor MÁS ALTO. Portado 1:1 desde
/// CabinaFletes.Api/Services/PropuestaTarifaService.cs del proyecto original.
/// Reemplaza a PropuestaTarifaNoMigradaAdapter (ver PENDIENTES.md/MIGRACION.md):
/// cierra el bloqueo de "Aplicar propuestas" en Cotizaciones.
///
/// Los cargues masivos se procesan en PARALELO con scope propio de DbContext por
/// trabajador (los contextos no son thread-safe), concurrencia limitada.
/// </summary>
public sealed class PropuestaTarifaAdapter : IPropuestaTarifaPort
{
    private const int Concurrencia = 10;
    private const int MaxFilasFuente = 500;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly CabinaFletesDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IAjustesVigenciaPlazaRepository _ajustes;
    private readonly IClavesSoloSicetacPort _reglas;
    private readonly bool _dwConfigurado;
    private readonly bool _dcConfigurado;

    public PropuestaTarifaAdapter(
        CabinaFletesDbContext db, IServiceProvider sp, IServiceScopeFactory scopes,
        IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes,
        IClavesSoloSicetacPort reglas)
    {
        _db = db;
        _scopes = scopes;
        _dwConfigurado = sp.GetService<DwSiceDbContext>() is not null;
        _dcConfigurado = sp.GetService<DbSolucionesTiDbContext>() is not null;
        _cache = cache;
        _clock = clock;
        _ajustes = ajustes;
        _reglas = reglas;
    }

    public async Task<PropuestaSolicitudDto> ProponerAsync(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await _db.Solicitudes.AsNoTracking()
            .Include(s => s.Lineas).ThenInclude(l => l.Ruta)
            .Include(s => s.Lineas).ThenInclude(l => l.ConfiguracionVehiculo)
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw new NotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        var advertencias = new List<string>();
        if (!_dwConfigurado) advertencias.Add("Fuente SICE-TAC no configurada (ConnectionStrings:DwSice).");
        if (!_dcConfigurado) advertencias.Add("Fuentes SAP no configuradas (ConnectionStrings:DbSolucionesTi).");

        var lineas = solicitud.Lineas.ToList();

        var ajustes = await _ajustes.MapaAsync(ct);
        var soloSice = await _reglas.ClavesRutaAsync(ct);

        static string Huella(LineaSolicitud l) => string.Join('|',
            l.Ruta?.Origen, l.Ruta?.Destino, l.ConfiguracionVehiculo?.TipoVehiculo, l.ConfiguracionVehiculo?.Carroceria,
            l.Tipologia, l.Peso, l.LineaNegocio, l.ClaseViaje, l.ClaseContenedor,
            l.Exclusivo, l.Consolidado, l.RetornaContenedor, l.CiudadDevolucionContenedor);

        var grupos = lineas.GroupBy(Huella).ToList();

        using var sem = new SemaphoreSlim(Concurrencia);
        var tareas = grupos.Select(async grupo =>
        {
            await sem.WaitAsync(ct);
            try
            {
                using var scope = _scopes.CreateScope();
                var dw = _dwConfigurado ? scope.ServiceProvider.GetService<DwSiceDbContext>() : null;
                var dc = _dcConfigurado ? scope.ServiceProvider.GetService<DbSolucionesTiDbContext>() : null;
                var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();
                var dto = await ProponerLineaAsync(dw, dc, tarifario, grupo.First(), ajustes, soloSice, ct);
                return grupo.Select(l => dto with { LineaId = l.Id }).ToList();
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        var resultados = (await Task.WhenAll(tareas)).SelectMany(x => x).ToList();
        return new PropuestaSolicitudDto(solicitud.Id, resultados, advertencias);
    }

    private async Task<PropuestaLineaDto> ProponerLineaAsync(
        DwSiceDbContext? dw, DbSolucionesTiDbContext? dc, ITarifarioSapPort tarifario,
        LineaSolicitud linea, IReadOnlyDictionary<int, DateTime> ajustesVigencia,
        IReadOnlySet<string> soloSice, CancellationToken ct)
    {
        if (linea.Ruta is null)
        {
            return new PropuestaLineaDto(
                linea.Id, null, null, linea.Peso, null, null, null, null, null,
                "La línea no tiene ruta definida: no es posible buscar en la Biblioteca.");
        }

        var origen = Normalizar(linea.Ruta.Origen);
        var destino = Normalizar(linea.Ruta.Destino);
        var vehiculo = linea.ConfiguracionVehiculo?.TipoVehiculo;

        // Clave "ORIGEN|DESTINO" sin normalizar: debe coincidir exactamente con
        // IClavesSoloSicetacPort.ClavesRutaAsync (Infrastructure/Repositories/EfReglasSoloSicetacRepository).
        if (soloSice.Contains($"{linea.Ruta.Origen}|{linea.Ruta.Destino}"))
        {
            var sice = dw is null ? null : await CandidatoSicetacAsync(dw, origen, destino, vehiculo, linea.Tipologia, linea.ConfiguracionVehiculo?.Carroceria, ct);
            var justif = sice is not null
                ? $"Ruta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({sice.Valor:C0}) sin comparar con plaza ni tarifario."
                : "Ruta marcada como SOLO SICE-TAC, pero no hay piso SICE-TAC para la ruta/periodo: la línea requiere atención manual.";
            return new PropuestaLineaDto(
                linea.Id, $"{linea.Ruta.Origen} → {linea.Ruta.Destino}", vehiculo, linea.Peso,
                sice, null, null, sice?.Valor, sice is null ? null : "sicetac", justif);
        }

        ResultadoTarifarioSap? oficial = null;
        if (linea.ClaseViaje is not null && dc is not null)
        {
            oficial = await tarifario.ConsultarAsync(new ConsultaTarifarioSap(
                linea.Ruta.Origen, linea.Ruta.Destino,
                linea.LineaNegocio, linea.ClaseViaje, linea.ClaseContenedor,
                vehiculo, linea.ConfiguracionVehiculo?.Carroceria,
                linea.Peso, linea.Exclusivo, linea.Consolidado,
                linea.RetornaContenedor, linea.CiudadDevolucionContenedor), ct);
        }

        CandidatoTarifaDto? sicetac;
        CandidatoTarifaDto? plaza;
        var vencida = oficial?.Vencida == true;
        if (oficial is not null)
        {
            plaza = oficial.Tarifa;
            sicetac = oficial.SicetacOficial;
        }
        else
        {
            sicetac = dw is null ? null : await CandidatoSicetacAsync(dw, origen, destino, vehiculo, linea.Tipologia, linea.ConfiguracionVehiculo?.Carroceria, ct);
            plaza = dc is null ? null : await CandidatoSapAsync(dc, dc.TarifasPlazaSap, "plaza", origen, destino, vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct);
        }
        var historico = dc is null ? null : await CandidatoSapAsync(dc, dc.TarifasLogSap, "historico_sap", origen, destino, vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct);

        var (valor, fuente, justificacion) = DecidirConTarifario(sicetac, plaza, historico, vencida);

        if (oficial?.Retorno is { } retorno)
            justificacion += $" Retorno del contenedor a {linea.CiudadDevolucionContenedor}: {retorno.Valor:C0} (línea devolución, exclusivo).";
        else if (linea.RetornaContenedor)
            justificacion += " El viaje retorna contenedor pero no se encontró tarifa de devolución (línea 6 exclusiva) para la ruta.";

        return new PropuestaLineaDto(
            linea.Id,
            $"{linea.Ruta.Origen} → {linea.Ruta.Destino}",
            vehiculo,
            linea.Peso,
            sicetac, plaza, historico,
            valor, fuente, justificacion,
            oficial?.Retorno);
    }

    // ------------------------------------------------------------- decisión

    internal static (decimal?, string?, string) Decidir(
        CandidatoTarifaDto? sicetac, CandidatoTarifaDto? plaza, CandidatoTarifaDto? historico)
    {
        if (plaza is not null && sicetac is not null)
        {
            var mayor = plaza.Valor >= sicetac.Valor ? plaza : sicetac;
            var diff = Math.Abs(plaza.Valor - sicetac.Valor);
            var pct = sicetac.Valor > 0 ? Math.Round(diff / sicetac.Valor * 100, 1) : 0;
            var comparacion = plaza.Valor >= sicetac.Valor
                ? $"El flete de plaza ({plaza.Valor:C0}) supera el piso SICE-TAC ({sicetac.Valor:C0}) en {pct}%: se propone plaza."
                : $"El piso SICE-TAC ({sicetac.Valor:C0}) supera el flete de plaza ({plaza.Valor:C0}) en {pct}%: se propone SICE-TAC para no cotizar bajo el piso.";
            return (mayor.Valor, mayor.Fuente, comparacion);
        }
        if (plaza is not null)
            return (plaza.Valor, plaza.Fuente, "Solo se encontró flete de plaza; sin piso SICE-TAC comparable para la ruta.");
        if (sicetac is not null)
            return (sicetac.Valor, sicetac.Fuente, "Solo se encontró piso SICE-TAC; sin tarifa de plaza vigente para la ruta.");

        var referencia = historico is not null
            ? $" Referencia disponible: histórico SAP {historico.Valor:C0} (el Administrador de Fletes decide manualmente)."
            : string.Empty;
        return (null, null,
            "Sin flete de plaza ni piso SICE-TAC para la ruta: la línea requiere atención manual del " +
            "Administrador de Fletes en la bandeja de entrada, según las reglas de priorización." + referencia);
    }

    internal static (decimal?, string?, string) DecidirConTarifario(
        CandidatoTarifaDto? sicetac, CandidatoTarifaDto? plaza, CandidatoTarifaDto? historico, bool tarifarioVencido)
    {
        if (!tarifarioVencido)
            return Decidir(sicetac, plaza, historico);

        var (valor, fuente, _) = Decidir(sicetac, plaza: null, historico);
        var justificacion = sicetac is not null
            ? $"El tarifario está INACTIVO (vigencia vencida): se propone el piso SICE-TAC vigente ({sicetac.Valor:C0}); " +
              "nunca cotiza por debajo del piso legal. El tarifario vencido queda solo como referencia."
            : "La tarifa del tarifario está INACTIVA (vigencia vencida) y no hay piso SICE-TAC vigente para la ruta: " +
              "requiere revisión manual del Administrador de Fletes.";
        return (valor, fuente, justificacion);
    }

    // ------------------------------------------------------------- SICE-TAC (DWSice)

    public async Task<CandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<DwSiceDbContext>();
        if (dw is null) return null;

        return await CandidatoSicetacAsync(
            dw, Normalizar(ciudadLevante), Normalizar(origen), configuracion,
            "VACIO", tipologia: null, carroceria: null, ct);
    }

    private Task<CandidatoTarifaDto?> CandidatoSicetacAsync(
        DwSiceDbContext dw, string origen, string destino, string? vehiculo,
        TipologiaCarga? tipologia, string? carroceria, CancellationToken ct)
        => CandidatoSicetacAsync(dw, origen, destino, vehiculo, "CARGADO", tipologia, carroceria, ct);

    private async Task<CandidatoTarifaDto?> CandidatoSicetacAsync(
        DwSiceDbContext dw, string origen, string destino, string? vehiculo, string condicionCarga,
        TipologiaCarga? tipologia, string? carroceria, CancellationToken ct)
    {
        var periodo = await PeriodoSiceAsync(dw, ct);
        if (periodo == 0) return null;

        var baseQ = dw.RutasSicetac.AsNoTracking().Where(r =>
            r.Periodo == periodo &&
            r.CondicionCarga == condicionCarga &&
            EF.Functions.Like(r.NombreRuta, $"%{origen}%-%{destino}%"));

        var criterio = $"periodo {periodo} · {condicionCarga} · ruta ~ '{origen} - {destino}'";
        var claveBase = $"sice-filas:{periodo}:{condicionCarga}:{origen}:{destino}";

        RutaSicetacDw? fila = null;
        if (!string.IsNullOrWhiteSpace(vehiculo))
        {
            var config = vehiculo.Trim().ToUpper();
            fila = SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:{config}", baseQ.Where(r => r.Configuracion == config), ct), tipologia, carroceria);
            if (fila is not null) criterio += $" · configuración {config}";
        }
        fila ??= SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:*", baseQ, ct), tipologia, carroceria);
        if (fila is null) return null;

        var valor = fila.ValorSiceConHora ?? fila.Valor;
        return new CandidatoTarifaDto(
            "sicetac", valor,
            $"{fila.NombreRuta} · {fila.Configuracion} · {fila.NombreTipoCarga} · {fila.NombreUnidadTransporte}",
            criterio, 1);
    }

    internal static RutaSicetacDw? SeleccionarSice(
        List<RutaSicetacDw> filas, TipologiaCarga? tipologia, string? carroceria)
    {
        if (filas.Count == 0) return null;

        filas = filas.Where(r => (r.ValorSiceConHora ?? r.Valor) > 0).ToList();
        if (filas.Count == 0) return null;

        var tipo = MapearTipoCargaSice(tipologia);
        var porTipo = tipo is not null
            ? filas.Where(r => string.Equals(r.NombreTipoCarga, tipo, StringComparison.OrdinalIgnoreCase)).ToList()
            : [];
        if (porTipo.Count == 0)
            porTipo = filas.Where(r => string.Equals(r.NombreTipoCarga, "General", StringComparison.OrdinalIgnoreCase)).ToList();
        var cand = porTipo.Count > 0 ? porTipo : filas;

        if (!string.IsNullOrWhiteSpace(carroceria))
        {
            var porUnidad = cand.Where(r => string.Equals(r.NombreUnidadTransporte, carroceria.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (porUnidad.Count > 0) cand = porUnidad;
        }

        return cand
            .OrderBy(r => string.Equals(r.ViaEstandar, "SI", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.HorasRecorrido ?? decimal.MaxValue)
            .ThenBy(r => r.Valor)
            .FirstOrDefault();
    }

    private static string? MapearTipoCargaSice(TipologiaCarga? t) => t switch
    {
        TipologiaCarga.GranelSolido => "Granel Sólido",
        TipologiaCarga.GranelLiquido => "Granel líquido",
        TipologiaCarga.Refrigerados => "Carga Refrigerada",
        TipologiaCarga.Contenedor => "Contenedor cargado",
        TipologiaCarga.CargaSeca or TipologiaCarga.CargaSuelta or TipologiaCarga.Alimentos => "General",
        _ => null,
    };

    private async Task<List<RutaSicetacDw>> FilasSiceCacheadasAsync(string clave, IQueryable<RutaSicetacDw> q, CancellationToken ct) =>
        (await _cache.GetOrCreateAsync(clave, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await q.ToListAsync(ct);
        }))!;

    private async Task<int> PeriodoSiceAsync(DwSiceDbContext dw, CancellationToken ct)
    {
        var periodo = await _cache.GetOrCreateAsync("prop:sice-periodo", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await dw.RutasSicetac.Where(r => r.Periodo > 0).MaxAsync(r => (int?)r.Periodo, ct) ?? 0;
        });
        return periodo;
    }

    // ------------------------------------------------------------- SAP (plaza e histórico)

    private async Task<CandidatoTarifaDto?> CandidatoSapAsync<T>(
        DbSolucionesTiDbContext dc, IQueryable<T> tabla, string fuente, string origen, string destino,
        string? vehiculo, decimal? peso, bool soloVigentes,
        IReadOnlyDictionary<int, DateTime> ajustesVigencia, CancellationToken ct) where T : SapTarifaBase
    {
        var codigosOrigen = await CodigosDaneAsync(dc, origen, ct);
        var codigosDestino = await CodigosDaneAsync(dc, destino, ct);

        var query = tabla.Where(t =>
            (codigosOrigen.Contains(t.Origen) || EF.Functions.Like(t.Origen, $"%{origen}%")) &&
            (codigosDestino.Contains(t.Destino) || EF.Functions.Like(t.Destino, $"%{destino}%")));

        DateTime Efectiva(T t) => ajustesVigencia.TryGetValue(t.Id, out var v) ? v : t.Vigencia;

        var hoy = _clock.GetUtcNow().Date;
        var criterio = $"origen ~ '{origen}' · destino ~ '{destino}'";
        if (soloVigentes)
        {
            var idsAjustados = ajustesVigencia.Keys.ToList();
            query = query.Where(t => t.Vigencia >= hoy || idsAjustados.Contains(t.Id));
            criterio += " · vigencia efectiva >= hoy";
        }

        if (!string.IsNullOrWhiteSpace(vehiculo))
        {
            var idVehiculo = await IdVehiculoSapAsync(dc, vehiculo, ct);
            if (idVehiculo is not null)
            {
                var conVehiculo = query.Where(t => t.TipoVehiculo == idVehiculo);
                if (await conVehiculo.AnyAsync(ct))
                {
                    query = conVehiculo;
                    criterio += $" · vehículo {vehiculo.Trim().ToUpper()}";
                }
            }
        }

        var ajustesClave = ajustesVigencia.Count == 0 ? "" : string.Join('-', ajustesVigencia.Keys.OrderBy(k => k));
        var claveFetch = $"sap-filas:{fuente}:{origen}:{destino}:{vehiculo}:{soloVigentes}:{hoy:yyyyMMdd}:{ajustesClave}";
        var filas = (await _cache.GetOrCreateAsync(claveFetch, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
            return await query.AsNoTracking().OrderByDescending(t => t.Id).Take(MaxFilasFuente).ToListAsync(ct);
        }))!;
        if (filas.Count == 0) return null;

        if (soloVigentes)
        {
            filas = filas.Where(t => Efectiva(t) >= hoy).ToList();
            if (filas.Count == 0) return null;
        }

        if (filas is List<SapTarifaLog> log)
        {
            filas = log
                .GroupBy(l => l.IdTarifa)
                .Select(g => g.OrderByDescending(x => x.Id).First())
                .Cast<T>()
                .ToList();
            criterio += " · último registro por tarifa";
        }

        var candidatas = filas
            .Select(t => (Fila: t, Valor: ParseValor(t.Valor)))
            .Where(x => x.Valor is > 0)
            .ToList();
        if (candidatas.Count == 0) return null;

        if (peso is not null)
        {
            var enRango = candidatas.Where(x =>
                x.Fila.RangoPesoInf.HasValue && x.Fila.RangoPesoSup is > 0 &&
                peso >= x.Fila.RangoPesoInf && peso <= x.Fila.RangoPesoSup).ToList();
            if (enRango.Count > 0)
            {
                candidatas = enRango;
                criterio += $" · rango de peso cubre {peso:0} kg";
            }
        }

        var top = candidatas.OrderByDescending(x => x.Valor).First();
        var vigenciaEfectiva = Efectiva(top.Fila);
        var ajustada = ajustesVigencia.ContainsKey(top.Fila.Id) ? " (ajustada)" : "";
        return new CandidatoTarifaDto(
            fuente, top.Valor!.Value,
            $"{top.Fila.Origen} → {top.Fila.Destino} · vigencia {vigenciaEfectiva:yyyy-MM-dd}{ajustada}" +
                (top.Fila is SapTarifaPlaza { DetallesTarifa: { Length: > 0 } det } ? $" · {det}" : ""),
            criterio, candidatas.Count);
    }

    private async Task<List<string>> CodigosDaneAsync(DbSolucionesTiDbContext dc, string ciudad, CancellationToken ct)
    {
        var municipios = await _cache.GetOrCreateAsync("prop:dane-municipios", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var todos = await dc.CentrosPobladosDane
                .Select(d => new { d.CodigoCentroPoblado, d.NombreMunicipio })
                .ToListAsync(ct);
            return todos.Select(d => (Codigo: d.CodigoCentroPoblado.Trim(), Nombre: Normalizar(d.NombreMunicipio))).ToList();
        });

        return municipios!
            .Where(m => m.Nombre.Contains(ciudad) || ciudad.Contains(m.Nombre))
            .Select(m => m.Codigo)
            .Take(200)
            .ToList();
    }

    private async Task<string?> IdVehiculoSapAsync(DbSolucionesTiDbContext dc, string codigoVehiculo, CancellationToken ct)
    {
        var porCodigo = await _cache.GetOrCreateAsync("prop:sap-vehiculos", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await dc.TiposVehiculo.Select(v => new { v.Id, v.Codigo }).ToListAsync(ct);
            return lista
                .GroupBy(v => v.Codigo.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id.ToString());
        });
        return porCodigo!.GetValueOrDefault(codigoVehiculo.Trim().ToUpperInvariant());
    }

    private static decimal? ParseValor(string valor) => ImporteParser.Parse(valor);

    private static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        var limpio = string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return limpio.ToUpperInvariant();
    }
}
