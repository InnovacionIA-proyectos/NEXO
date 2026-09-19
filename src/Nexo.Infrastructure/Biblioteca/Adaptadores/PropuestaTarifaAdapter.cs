using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Common;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.External;
using Nexo.Infrastructure.Persistence;
using Nexo.Infrastructure.ReglasSoloSicetac.Repositorios;
using Nexo.Mensajes.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Infrastructure.Biblioteca.Adaptadores;

/// <summary>
/// Motor de propuesta de tarifa: por cada línea de la solicitud consulta las tres
/// fuentes de la Biblioteca —piso SICE-TAC (DWSice), tarifas de plaza SAP e
/// histórico SAP— y propone el valor MÁS ALTO. Portado 1:1 desde
/// Nexo.Api/Services/PropuestaTarifaService.cs del proyecto original.
/// Reemplaza a PropuestaTarifaNoMigradaAdapter (ver PENDIENTES.md/MIGRACION.md):
/// cierra el bloqueo de "Aplicar propuestas" en Cotizaciones.
///
/// Los cargues masivos se procesan en PARALELO con scope propio de DbContext por
/// trabajador (los contextos no son thread-safe), concurrencia limitada.
/// </summary>
public sealed class PropuestaTarifaAdapter : IPropuestaTarifaPort, IPropuestaDeLineaPort
{
    private const int Concurrencia = 10;
    private const int MaxFilasFuente = 500;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly NexoDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IAjustesVigenciaPlazaRepository _ajustes;
    private readonly IClavesSoloSicetacPort _reglas;
    private readonly ISapTarifaFleteEnVivoPort _sapEnVivo;
    private readonly IPublicadorDeMensajes _publicador;
    private readonly bool _dwConfigurado;
    private readonly bool _dcConfigurado;

    public PropuestaTarifaAdapter(
        NexoDbContext db, IServiceProvider sp, IServiceScopeFactory scopes,
        IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes,
        IClavesSoloSicetacPort reglas, ISapTarifaFleteEnVivoPort sapEnVivo, IPublicadorDeMensajes publicador)
    {
        _db = db;
        _scopes = scopes;
        _dwConfigurado = sp.GetService<DwSiceDbContext>() is not null;
        _dcConfigurado = sp.GetService<DbSolucionesTiDbContext>() is not null;
        _cache = cache;
        _clock = clock;
        _ajustes = ajustes;
        _reglas = reglas;
        _sapEnVivo = sapEnVivo;
        _publicador = publicador;
    }

    /// <summary>
    /// Propuesta de UNA línea. Es el camino que usa el auto-costeo asíncrono.
    ///
    /// Reusa la misma maquinaria que el camino por solicitud —las mismas fuentes, la
    /// misma caché, la misma regla— pero carga una sola línea. Con eso una excepción
    /// consultando una fuente queda contenida en esa línea en vez de llevarse las 300,
    /// que es lo que exige la regla inviolable 6.
    /// </summary>
    public async Task<ResultadoDeCosteo> ProponerParaLineaAsync(Guid lineaId, CancellationToken ct = default)
    {
        var linea = await _db.LineasSolicitud.AsNoTracking()
            .Include(l => l.Ruta)
            .Include(l => l.ConfiguracionVehiculo)
            .FirstOrDefaultAsync(l => l.Id == lineaId, ct)
            ?? throw new NotFoundException("LINEA_NO_ENCONTRADA", "Línea de solicitud no encontrada.");

        var ajustes = await _ajustes.MapaAsync(ct);
        var soloSice = await _reglas.ClavesRutaVehiculoAsync(ct);

        using var scope = _scopes.CreateScope();
        var dw = _dwConfigurado ? scope.ServiceProvider.GetService<DwSiceDbContext>() : null;
        var dc = _dcConfigurado ? scope.ServiceProvider.GetService<DbSolucionesTiDbContext>() : null;
        var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();

        var dto = await ProponerLineaAsync(dw, dc, tarifario, linea, ajustes, soloSice, ct);
        await PublicarVencidaSiAplicaAsync(dto, ct);
        return new ResultadoDeCosteo(dto.ValorPropuesto, dto.FuentePropuesta, dto.Justificacion);
    }

    /// <summary>
    /// Sección 9 del documento de entendimiento: alerta de urgencia Alta cuando el
    /// costeo descarta una tarifa de plaza por vencida (RN-02). Solo desde el camino
    /// por línea (ProponerParaLineaAsync), nunca desde el camino por solicitud
    /// (ProponerAsync): ese último corre sus grupos en paralelo sobre el MISMO
    /// DbContext, y el publicador de mensajes escribe en él — hacerlo ahí sería una
    /// carrera de datos. Deduplicado por día: un pliego con 300 líneas de la misma
    /// ruta vencida no debe mandar 300 correos.
    /// </summary>
    private async Task PublicarVencidaSiAplicaAsync(PropuestaLineaDto dto, CancellationToken ct)
    {
        if (!dto.Vencida) return;

        var clave = $"tarifa-vencida-avisada:{dto.Ruta}|{dto.Vehiculo}|{_clock.GetUtcNow():yyyy-MM-dd}";
        if (_cache.TryGetValue(clave, out _)) return;
        _cache.Set(clave, true, TimeSpan.FromHours(24));

        await _publicador.PublicarAsync(
            new TarifaVencidaDetectada(dto.LineaId, dto.Justificacion, _clock.GetUtcNow()), ct);
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
        var soloSice = await _reglas.ClavesRutaVehiculoAsync(ct);

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
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Regla inviolable 6: un grupo que falla (fuente caída, timeout) NO puede
                // llevarse los demás. Task.WhenAll falla rápido y descarta hasta los grupos
                // que ya habían terminado bien si uno solo lanza — por eso el catch va AQUÍ,
                // por grupo, y no alrededor del WhenAll. Issue #131.
                var primera = grupo.First();
                var justificacion = $"No se pudo consultar la Biblioteca para esta línea: {ex.Message}";
                return grupo.Select(l => new PropuestaLineaDto(
                    l.Id, $"{primera.Ruta?.Origen} → {primera.Ruta?.Destino}", primera.ConfiguracionVehiculo?.TipoVehiculo,
                    l.Peso, null, null, null, null, null, justificacion)).ToList();
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

        // Clave canónica de Domain, la MISMA que usan quien guarda la regla y el
        // repositorio que las lee. Antes se comparaban los valores crudos de la ruta
        // contra claves ya normalizadas, y la regla no se aplicaba en silencio: ver
        // ClaveDeRutaTests en Domain.Tests y el comentario de cierre de la issue #16.
        //
        // Issue #17: las 3 llaves (ruta + tipo de vehículo), no solo las 2 de la ruta -
        // antes, marcar un único tipo de vehículo como "solo SICE-TAC" apagaba la
        // comparación contra plaza para TODOS los tipos de vehículo de esa ruta.
        if (soloSice.Contains(Texto.ClaveRutaVehiculo(linea.Ruta.Origen, linea.Ruta.Destino, vehiculo)))
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

        CandidatoTarifaDto? plaza;
        var vencida = oficial?.Vencida == true;
        if (oficial is not null)
        {
            plaza = oficial.Tarifa;
        }
        else
        {
            plaza = dc is null ? null : await CandidatoSapAsync(dc, dc.TarifasPlazaSap, "plaza", origen, destino, vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct);
        }

        // Issue #175/#176: TarifarioSapAdapter.SicetacOficialAsync (réplica 1:1 de la
        // lógica SAP legada) exige match exacto de Configuracion/TipoCarga/
        // UnidadTransporte/ViaEstandar sin fallback, y solo aplica a Contenedor/Cupo
        // (nunca a CargaSuelta/Extradimensionado) - eso deja sin piso automático rutas
        // que sí existen y son encontrables (reproducido con Medellín-Guarne),
        // obligando a marcar "Solo SICE-TAC" a mano para que aparezcan (la ruta
        // siempre fue encontrable: esa marca usa CandidatoSicetacAsync, el selector
        // tolerante de abajo). Si el camino oficial no encuentra nada, se reintenta con
        // ESE MISMO selector antes de rendirse, así el camino normal encuentra como
        // mínimo lo mismo que "Solo SICE-TAC". El camino oficial se prueba primero y no
        // se toca: sigue gobernando sin cambios cuando sí encuentra algo (verificado
        // contra datos reales en tests/QA/hu-13.4-verificacion-reglas-negocio-datos-reales).
        var sicetac = oficial?.SicetacOficial;
        if (sicetac is null && dw is not null)
            sicetac = await CandidatoSicetacAsync(dw, origen, destino, vehiculo, linea.Tipologia, linea.ConfiguracionVehiculo?.Carroceria, ct);

        var historico = dc is null ? null : await CandidatoSapAsync(dc, dc.TarifasLogSap, "historico_sap", origen, destino, vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct);

        // S2-HU-09.1/09.3 (#31/#33): conexión SAP NUEVA (plaza en vivo), todavía no
        // construida por "el equipo de conexiones". Con SapTarifaFleteEnVivo:Modo en
        // Deshabilitado (default) o Real-sin-adaptador-todavía, _sapEnVivo.Configurado
        // es false y esta rama nunca corre — la ruta de arriba (dc.TarifasPlazaSap, la
        // fuente REAL) queda exactamente igual que antes de esta HU. Solo con
        // Modo=Mock (u, en el futuro, un adaptador real) se sustituye la plaza — nunca
        // el piso SICE-TAC ni el histórico, y "vencida" no aplica a una consulta en
        // vivo (su propio contrato: la respuesta siempre es la vigente ahora).
        if (_sapEnVivo.Configurado)
        {
            var enVivo = await _sapEnVivo.ConsultarPlazaAsync(new ConsultaTarifarioSap(
                linea.Ruta.Origen, linea.Ruta.Destino,
                linea.LineaNegocio, linea.ClaseViaje, linea.ClaseContenedor,
                vehiculo, linea.ConfiguracionVehiculo?.Carroceria,
                linea.Peso, linea.Exclusivo, linea.Consolidado,
                linea.RetornaContenedor, linea.CiudadDevolucionContenedor), ct);
            if (enVivo is not null)
            {
                plaza = enVivo;
                vencida = false;
            }
        }

        var decision = ReglaDeCosteo.DecidirConTarifario(
            ADominio(sicetac), ADominio(plaza), ADominio(historico), vencida);
        var (valor, fuente, justificacion) = (decision.Valor, decision.Fuente, decision.Justificacion);

        // Issue #169: cuando la propuesta final es plaza y ese candidato viene del
        // fallback conservador (no un match exacto por vehículo/peso), la advertencia
        // se anexa a la MISMA justificación que ya se le muestra al usuario - no hace
        // falta que el frontend sepa leer un campo aparte para enterarse.
        if (fuente == "plaza" && plaza?.Advertencia is { } advertenciaPlaza)
            justificacion += $" ⚠ {advertenciaPlaza}";

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
            oficial?.Retorno,
            Vencida: vencida);
    }

    // La decisión vive en Domain (Costeo/Reglas/ReglaDeCosteo): son invariantes del
    // negocio, no de este adaptador. Aquí solo se traen los candidatos y se traducen
    // los DTO de aplicación a los objetos de valor del dominio.

    private static CandidatoDeTarifa? ADominio(CandidatoTarifaDto? c) =>
        c is null ? null : new CandidatoDeTarifa(c.Fuente, c.Valor);

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
