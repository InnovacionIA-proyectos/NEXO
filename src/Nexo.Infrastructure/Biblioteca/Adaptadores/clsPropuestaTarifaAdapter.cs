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
using Nexo.Domain.Costeo.Entidades;
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
/// Reemplaza a PropuestaTarifaNoMigradaAdapter: cierra el bloqueo de
/// "Aplicar propuestas" en Cotizaciones.
///
/// Los cargues masivos se procesan en PARALELO con scope propio de DbContext por
/// trabajador (los contextos no son thread-safe), concurrencia limitada.
/// </summary>
public sealed class clsPropuestaTarifaAdapter : IPropuestaTarifaPort, IPropuestaDeLineaPort
{
    private const int Concurrencia = 10;
    private const int MaxFilasFuente = 500;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private readonly clsNexoDbContext _db;
    private readonly IServiceScopeFactory _scopes;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly IAjustesVigenciaPlazaRepository _ajustes;
    private readonly IClavesSoloSicetacPort _reglas;
    private readonly ISapTarifaFleteEnVivoPort _sapEnVivo;
    private readonly IPublicadorDeMensajes _publicador;
    private readonly bool _dwConfigurado;
    private readonly bool _dcConfigurado;

    public clsPropuestaTarifaAdapter(
        clsNexoDbContext db, IServiceProvider sp, IServiceScopeFactory scopes,
        IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes,
        IClavesSoloSicetacPort reglas, ISapTarifaFleteEnVivoPort sapEnVivo, IPublicadorDeMensajes publicador)
    {
        _db = db;
        _scopes = scopes;
        _dwConfigurado = sp.GetService<clsDwSiceDbContext>() is not null;
        _dcConfigurado = sp.GetService<clsDbSolucionesTiDbContext>() is not null;
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
    public async Task<clsResultadoDeCosteo> ProponerParaLineaAsync(Guid lineaId, CancellationToken ct = default)
    {
        var linea = await _db.LineasSolicitud.AsNoTracking()
            .Include(l => l.clsRuta).ThenInclude(r => r!.OrigenMunicipio)
            .Include(l => l.clsRuta).ThenInclude(r => r!.DestinoMunicipio)
            .Include(l => l.clsConfiguracionVehiculo)
            .Include(l => l.clsSolicitud)
            .FirstOrDefaultAsync(l => l.Id == lineaId, ct)
            ?? throw new clsNotFoundException("LINEA_NO_ENCONTRADA", "Línea de solicitud no encontrada.");

        var ajustes = await _ajustes.MapaAsync(ct);
        var soloSice = await _reglas.ClavesRutaVehiculoAsync(ct);

        // S4-HU-14.1/#221: mismas horas reales que usa el camino por solicitud.
        // S4-HU-15.5/#251: si ESTA línea ya tiene sus propias horas resueltas (captura
        // posterior a #251), tienen prioridad sobre las de la solicitud completa —
        // líneas capturadas ANTES de #251 (HorasCargue/HorasDescargue nulas en la
        // línea) siguen usando el valor de la solicitud, sin cambio de comportamiento.
        var tiempos = clsCondicionesServicioDto.Desde(linea.clsSolicitud?.CondicionesServicio).TiemposLogisticos!;
        var horasSolicitud = HorasEfectivasDeLinea(linea, tiempos.HorasCargue + tiempos.HorasDescargue);

        using var scope = _scopes.CreateScope();
        var dw = _dwConfigurado ? scope.ServiceProvider.GetService<clsDwSiceDbContext>() : null;
        var dc = _dcConfigurado ? scope.ServiceProvider.GetService<clsDbSolucionesTiDbContext>() : null;
        var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();

        var dto = await ProponerLineaAsync(dw, dc, tarifario, linea, ajustes, soloSice, horasSolicitud, ct);
        await PublicarVencidaSiAplicaAsync(dto, ct);
        return new clsResultadoDeCosteo(dto.ValorPropuesto, dto.FuentePropuesta, dto.Justificacion);
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
    private async Task PublicarVencidaSiAplicaAsync(clsPropuestaLineaDto dto, CancellationToken ct)
    {
        if (!dto.Vencida) return;

        var clave = $"tarifa-vencida-avisada:{dto.clsRuta}|{dto.Vehiculo}|{_clock.GetUtcNow():yyyy-MM-dd}";
        if (_cache.TryGetValue(clave, out _)) return;
        _cache.Set(clave, true, TimeSpan.FromHours(24));

        await _publicador.PublicarAsync(
            new clsTarifaVencidaDetectada(dto.LineaId, dto.Justificacion, _clock.GetUtcNow()), ct);
    }

    public async Task<clsPropuestaSolicitudDto> ProponerAsync(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await _db.Solicitudes.AsNoTracking()
            .Include(s => s.Lineas).ThenInclude(l => l.clsRuta).ThenInclude(r => r!.OrigenMunicipio)
            .Include(s => s.Lineas).ThenInclude(l => l.clsRuta).ThenInclude(r => r!.DestinoMunicipio)
            .Include(s => s.Lineas).ThenInclude(l => l.clsConfiguracionVehiculo)
            .FirstOrDefaultAsync(s => s.Id == solicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");

        var advertencias = new List<string>();
        if (!_dwConfigurado) advertencias.Add("Fuente SICE-TAC no configurada (ConnectionStrings:DwSice).");
        if (!_dcConfigurado) advertencias.Add("Fuentes SAP no configuradas (ConnectionStrings:DbSolucionesTi).");

        var lineas = solicitud.Lineas.ToList();

        var ajustes = await _ajustes.MapaAsync(ct);
        var soloSice = await _reglas.ClavesRutaVehiculoAsync(ct);

        // S4-HU-14.1/#221: horas reales capturadas en la solicitud (Cargue+Descargue),
        // para recalcular el piso SICE-TAC en vez de asumir las horas pactadas.
        // S4-HU-15.5/#251: ahora puede variar POR LÍNEA — HorasEfectivasDeLinea prefiere
        // las de la línea si ya están resueltas (captura posterior a #251), si no cae
        // al valor de la solicitud completa (líneas viejas, sin cambio de comportamiento).
        var tiempos = clsCondicionesServicioDto.Desde(solicitud.CondicionesServicio).TiemposLogisticos!;
        var horasSolicitud = tiempos.HorasCargue + tiempos.HorasDescargue;

        // Horas entra en la huella: dos líneas idénticas en todo lo demás pero con horas
        // distintas pueden costear distinto (clsPisoConHoras.Calcular) — agruparlas
        // igual les daría a ambas el piso de UNA sola de las dos.
        string Huella(clsLineaSolicitud l) => string.Join('|',
            l.clsRuta?.Origen, l.clsRuta?.Destino, l.clsConfiguracionVehiculo?.TipoVehiculo, l.clsConfiguracionVehiculo?.Carroceria,
            l.Tipologia, l.Peso, l.LineaNegocio, l.ClaseViaje, l.ClaseContenedor,
            l.Exclusivo, l.Consolidado, l.RetornaContenedor, l.CiudadDevolucionContenedor,
            HorasEfectivasDeLinea(l, horasSolicitud));

        var grupos = lineas.GroupBy(Huella).ToList();

        using var sem = new SemaphoreSlim(Concurrencia);
        var tareas = grupos.Select(async grupo =>
        {
            await sem.WaitAsync(ct);
            try
            {
                using var scope = _scopes.CreateScope();
                var dw = _dwConfigurado ? scope.ServiceProvider.GetService<clsDwSiceDbContext>() : null;
                var dc = _dcConfigurado ? scope.ServiceProvider.GetService<clsDbSolucionesTiDbContext>() : null;
                var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();
                var horasGrupo = HorasEfectivasDeLinea(grupo.First(), horasSolicitud);
                var dto = await ProponerLineaAsync(dw, dc, tarifario, grupo.First(), ajustes, soloSice, horasGrupo, ct);
                return grupo.Select(l => dto with { LineaId = l.Id }).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Regla inviolable 6: un grupo que falla (fuente caída, timeout) NO puede
                // llevarse los demás. Task.WhenAll falla rápido y descarta hasta los grupos
                // que ya habían terminado bien si uno solo lanza — por eso el catch va AQUÍ,
                // por grupo, y no alrededor del WhenAll.
                var primera = grupo.First();
                var justificacion = $"No se pudo consultar la Biblioteca para esta línea: {ex.Message}";
                return grupo.Select(l => new clsPropuestaLineaDto(
                    l.Id, $"{primera.clsRuta?.Origen} → {primera.clsRuta?.Destino}", primera.clsConfiguracionVehiculo?.TipoVehiculo,
                    l.Peso, null, null, null, null, null, justificacion)).ToList();
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        var resultados = (await Task.WhenAll(tareas)).SelectMany(x => x).ToList();
        return new clsPropuestaSolicitudDto(solicitud.Id, resultados, advertencias);
    }

    /// <summary>S4-HU-15.5/#251: prefiere las horas YA RESUELTAS de la línea (persistidas
    /// al capturar — clsResolucionHorasLinea.Resolver) sobre el valor único de la
    /// solicitud completa. Si la línea no las tiene (capturada antes de #251), cae al
    /// valor de la solicitud — mismo comportamiento que antes de este cambio.</summary>
    private static decimal HorasEfectivasDeLinea(clsLineaSolicitud linea, decimal horasSolicitud) =>
        linea.HorasCargue is { } hc && linea.HorasDescargue is { } hd ? hc + hd : horasSolicitud;

    private async Task<clsPropuestaLineaDto> ProponerLineaAsync(
        clsDwSiceDbContext? dw, clsDbSolucionesTiDbContext? dc, ITarifarioSapPort tarifario,
        clsLineaSolicitud linea, IReadOnlyDictionary<int, DateTime> ajustesVigencia,
        IReadOnlySet<string> soloSice, decimal horasSolicitud, CancellationToken ct)
    {
        if (linea.clsRuta is null)
        {
            return new clsPropuestaLineaDto(
                linea.Id, null, null, linea.Peso, null, null, null, null, null,
                "La línea no tiene ruta definida: no es posible buscar en la Biblioteca.");
        }

        var origen = Normalizar(linea.clsRuta.Origen);
        var destino = Normalizar(linea.clsRuta.Destino);
        var vehiculo = linea.clsConfiguracionVehiculo?.TipoVehiculo;

        // S5-HU-16.1/#254 + S5-HU-16.2/#255: código DANE real del municipio (tbl_municipios),
        // cuando la ruta ya resolvió contra el buscador de ciudades — null en rutas viejas
        // o con texto que no matcheó ningún municipio (ver clsResolucionDeCatalogos).
        var origenDane = linea.clsRuta.OrigenMunicipio?.CodigoDane;
        var destinoDane = linea.clsRuta.DestinoMunicipio?.CodigoDane;
        var origenMunicipioId = linea.clsRuta.OrigenMunicipioId;
        var destinoMunicipioId = linea.clsRuta.DestinoMunicipioId;

        // Clave por DANE (tbl_reglas_solo_sicetac.Origen/Destino SIEMPRE están en código
        // DANE — carga masiva S4-HU-09.2): antes comparaba contra el nombre de ciudad en
        // texto libre de la ruta y nunca coincidía (#255). Sin DANE resuelto para ambos
        // lados, la ruta no puede marcarse "solo SICE-TAC" por esta vía (igual que hoy,
        // sin regresión — antes tampoco coincidía nunca).
        if (origenDane is not null && destinoDane is not null &&
            soloSice.Contains(clsTexto.ClaveRutaVehiculo(origenDane, destinoDane, vehiculo)))
        {
            var sice = dw is null ? null : await CandidatoSicetacAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, linea.Tipologia, linea.clsConfiguracionVehiculo?.Carroceria, horasSolicitud, ct);
            var justif = sice is not null
                ? $"clsRuta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({sice.Valor:C0}) sin comparar con plaza ni tarifario."
                : "clsRuta marcada como SOLO SICE-TAC, pero no hay piso SICE-TAC para la ruta/periodo: la línea requiere atención manual.";
            return new clsPropuestaLineaDto(
                linea.Id, $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}", vehiculo, linea.Peso,
                sice, null, null, sice?.Valor, sice is null ? null : "sicetac", justif);
        }

        clsResultadoTarifarioSap? oficial = null;
        if (linea.ClaseViaje is not null && dc is not null)
        {
            oficial = await tarifario.ConsultarAsync(new clsConsultaTarifarioSap(
                linea.clsRuta.Origen, linea.clsRuta.Destino,
                linea.LineaNegocio, linea.ClaseViaje, linea.ClaseContenedor,
                vehiculo, linea.clsConfiguracionVehiculo?.Carroceria,
                linea.Peso, linea.Exclusivo, linea.Consolidado,
                linea.RetornaContenedor, linea.CiudadDevolucionContenedor, horasSolicitud), ct);
        }

        // El camino "oficial" (ITarifarioSapPort, ver comentario mas abajo) exige match
        // exacto de ClaseContenedor/peso/vehiculo sin fallback. Cuando no encuentra nada
        // (oficial es null, o oficial.Tarifa queda null porque el filtro exacto descarto
        // todo) se reintenta con el mismo selector tolerante que ya usa el camino "Solo
        // SICE-TAC" de arriba, antes de rendirse. Mismo patron que ya se aplico para el
        // piso SICE-TAC (ver sicetac mas abajo) y para el bug gemelo documentado en
        // docs/decisiones/matching-sicetac-oficial-vs-solo-sicetac.md.
        clsCandidatoTarifaDto? plaza = oficial?.Tarifa;
        var vencida = oficial?.Vencida == true;

        // S5-HU-16.3/#256: antes de caer a la tabla externa (SAP), se consulta primero
        // nuestra propia tbl_tarifas_plaza (sembrada en S4-HU-10.1/10.2) — match exacto
        // por Id de municipio, no por texto ni DANE. Solo corre si la ruta ya resolvió
        // ambos municipios (#254); si no, se salta directo al fallback externo, igual
        // que antes de esta issue.
        if (plaza is null && origenMunicipioId is not null && destinoMunicipioId is not null)
            plaza = await CandidatoPlazaPropiaAsync(origenMunicipioId.Value, destinoMunicipioId.Value, vehiculo, linea.Peso, ct);
        if (plaza is null && dc is not null)
            plaza = await CandidatoSapAsync(
                dc, dc.TarifasPlazaSap, "plaza", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct);

        // clsTarifarioSapAdapter.SicetacOficialAsync (réplica 1:1 de la lógica SAP
        // legada) exige match exacto de Configuracion/TipoCarga/UnidadTransporte/
        // ViaEstandar sin fallback, y solo aplica a Contenedor/Cupo (nunca a
        // CargaSuelta/Extradimensionado) - eso deja sin piso automático rutas que sí
        // existen y son encontrables (reproducido con Medellín-Guarne), obligando a
        // marcar "Solo SICE-TAC" a mano para que aparezcan (esa marca usa
        // CandidatoSicetacAsync, el selector tolerante de abajo). Si el camino oficial
        // no encuentra nada, se reintenta con ESE MISMO selector antes de rendirse. El
        // camino oficial se prueba primero y no se toca: sigue gobernando sin cambios
        // cuando sí encuentra algo (verificado contra datos reales en pruebas de QA de
        // reglas de negocio).
        var sicetac = oficial?.SicetacOficial;
        if (sicetac is null && dw is not null)
            sicetac = await CandidatoSicetacAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, linea.Tipologia, linea.clsConfiguracionVehiculo?.Carroceria, horasSolicitud, ct);

        // S5-HU-16.3/#256: mismo criterio que "plaza" — primero tbl_tarifas_plaza_historico
        // (propia), si no hay nada ahí, el log externo de SAP.
        clsCandidatoTarifaDto? historico = null;
        if (origenMunicipioId is not null && destinoMunicipioId is not null)
            historico = await CandidatoHistoricoPropioAsync(origenMunicipioId.Value, destinoMunicipioId.Value, vehiculo, linea.Peso, ct);
        if (historico is null && dc is not null)
            historico = await CandidatoSapAsync(
                dc, dc.TarifasLogSap, "historico_sap", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct, ventanaMeses: 3);

        // Conexión SAP nueva (plaza en vivo), todavía no construida por "el equipo de
        // conexiones". Con SapTarifaFleteEnVivo:Modo en Deshabilitado (default) o
        // Real-sin-adaptador-todavía, _sapEnVivo.Configurado es false y esta rama
        // nunca corre — la ruta de arriba (dc.TarifasPlazaSap, la fuente REAL) queda
        // exactamente igual que antes. Solo con Modo=Mock (u, en el futuro, un
        // adaptador real) se sustituye la plaza — nunca el piso SICE-TAC ni el
        // histórico, y "vencida" no aplica a una consulta en vivo (su propio
        // contrato: la respuesta siempre es la vigente ahora).
        if (_sapEnVivo.Configurado)
        {
            var enVivo = await _sapEnVivo.ConsultarPlazaAsync(new clsConsultaTarifarioSap(
                linea.clsRuta.Origen, linea.clsRuta.Destino,
                linea.LineaNegocio, linea.ClaseViaje, linea.ClaseContenedor,
                vehiculo, linea.clsConfiguracionVehiculo?.Carroceria,
                linea.Peso, linea.Exclusivo, linea.Consolidado,
                linea.RetornaContenedor, linea.CiudadDevolucionContenedor), ct);
            if (enVivo is not null)
            {
                plaza = enVivo;
                vencida = false;
            }
        }

        var decision = clsReglaDeCosteo.DecidirConTarifario(
            ADominio(sicetac), ADominio(plaza), ADominio(historico), vencida);
        var (valor, fuente, justificacion) = (decision.Valor, decision.Fuente, decision.Justificacion);

        // Cuando la propuesta final es plaza y ese candidato viene del fallback
        // conservador (no un match exacto por vehículo/peso), la advertencia se anexa
        // a la MISMA justificación que ya se le muestra al usuario - no hace falta que
        // el frontend sepa leer un campo aparte para enterarse.
        if (fuente == "plaza" && plaza?.Advertencia is { } advertenciaPlaza)
            justificacion += $" ⚠ {advertenciaPlaza}";

        if (oficial?.Retorno is { } retorno)
            justificacion += $" Retorno del contenedor a {linea.CiudadDevolucionContenedor}: {retorno.Valor:C0} (línea devolución, exclusivo).";
        else if (linea.RetornaContenedor)
            justificacion += " El viaje retorna contenedor pero no se encontró tarifa de devolución (línea 6 exclusiva) para la ruta.";

        return new clsPropuestaLineaDto(
            linea.Id,
            $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}",
            vehiculo,
            linea.Peso,
            sicetac, plaza, historico,
            valor, fuente, justificacion,
            oficial?.Retorno,
            Vencida: vencida);
    }

    // La decisión vive en Domain (Costeo/Reglas/clsReglaDeCosteo): son invariantes del
    // negocio, no de este adaptador. Aquí solo se traen los candidatos y se traducen
    // los DTO de aplicación a los objetos de valor del dominio.

    private static clsCandidatoDeTarifa? ADominio(clsCandidatoTarifaDto? c) =>
        c is null ? null : new clsCandidatoDeTarifa(c.Fuente, c.Valor);

    // ------------------------------------------------------------- SICE-TAC (DWSice)

    public async Task<clsCandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<clsDwSiceDbContext>();
        if (dw is null) return null;

        // Sin contexto de solicitud (es una estimación de tramo de posicionamiento, no de
        // una línea, sin clsRuta/municipio resuelto): horasSolicitud null, siempre usa el
        // ValorSiceConHora de DWSice, y sigue comparando por texto (sin DANE disponible).
        return await CandidatoSicetacAsync(
            dw, Normalizar(ciudadLevante), Normalizar(origen), origenDane: null, destinoDane: null, configuracion,
            "VACIO", tipologia: null, carroceria: null, horasSolicitud: null, ct);
    }

    private Task<clsCandidatoTarifaDto?> CandidatoSicetacAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane, string? vehiculo,
        TipologiaCarga? tipologia, string? carroceria, decimal? horasSolicitud, CancellationToken ct)
        => CandidatoSicetacAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, "CARGADO", tipologia, carroceria, horasSolicitud, ct);

    private async Task<clsCandidatoTarifaDto?> CandidatoSicetacAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane, string? vehiculo,
        string condicionCarga, TipologiaCarga? tipologia, string? carroceria, decimal? horasSolicitud, CancellationToken ct)
    {
        var (fila, criterio) = await ResolverFilaSiceAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, condicionCarga, tipologia, carroceria, ct);
        if (fila is null) return null;

        // S4-HU-14.1/#221: piso recalculado con las horas reales de la solicitud en vez
        // de asumir las pactadas — ver clsPisoConHoras para el criterio de fallback.
        var valor = clsPisoConHoras.Calcular(fila.Valor, fila.ValorHora, fila.HorasPactadas, fila.ValorSiceConHora, horasSolicitud);
        return new clsCandidatoTarifaDto(
            "sicetac", valor,
            $"{fila.NombreRuta} · {fila.Configuracion} · {fila.NombreTipoCarga} · {fila.NombreUnidadTransporte}",
            criterio, 1);
    }

    /// <summary>
    /// Piso SICE-TAC vigente con desglose de horas para Mesa de Costeo (issue #179):
    /// misma fuente DWSice que ya usa el motor automático, no la caché local que se
    /// retiró (import manual de Excel, sin evidencia de uso real y desactualizada).
    /// </summary>
    public async Task<clsPisoSicetacVigenteDto?> PisoSicetacVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct = default)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<clsDwSiceDbContext>();
        if (dw is null) return null;

        var (fila, _) = await ResolverFilaSiceAsync(
            dw, Normalizar(origen), Normalizar(destino), origenDane: null, destinoDane: null, vehiculo, "CARGADO", tipologia, carroceria, ct);
        if (fila is null) return null;

        return new clsPisoSicetacVigenteDto(
            fila.Periodo, fila.Valor, fila.ValorHora, fila.HorasPactadas,
            fila.Configuracion, fila.NombreTipoCarga, fila.NombreUnidadTransporte,
            fila.CondicionCarga ?? "CARGADO");
    }

    /// <summary>
    /// Resuelve la fila de DWSice: mismo camino para la propuesta automática y para
    /// Mesa de Costeo (<see cref="PisoSicetacVigenteAsync"/>) — una sola consulta a
    /// mantener, dos formas distintas de exponer el resultado.
    /// S5-HU-16.2/#255: con <paramref name="origenDane"/>/<paramref name="destinoDane"/>
    /// (tbl_municipios.CodigoDane) compara EXACTO contra
    /// tblRutasSicetac.ORIGEN/DESTINO — confirmado con datos reales que esa tabla
    /// guarda el mismo código DANE pero SIN el cero a la izquierda del departamento
    /// (tbl_municipios "05001000" ~ DWSice "5001000"), de ahí el TrimStart('0'). Sin
    /// DANE (ruta vieja sin backfill, o texto que no resolvió ningún municipio — ver
    /// #254) cae al LIKE de texto contra NOMBRERUTA, igual que antes de esta issue.
    /// La tabla tiene ~2.1 millones de filas por periodo: el filtro exacto por DANE,
    /// además de corregir el match, es muchísimo más barato que el LIKE.
    /// </summary>
    private async Task<(clsRutaSicetacDw? Fila, string Criterio)> ResolverFilaSiceAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane,
        string? vehiculo, string condicionCarga, TipologiaCarga? tipologia, string? carroceria, CancellationToken ct)
    {
        var periodo = await PeriodoSiceAsync(dw, ct);
        if (periodo == 0) return (null, string.Empty);

        IQueryable<clsRutaSicetacDw> baseQ;
        string criterio;
        string claveRuta;
        if (origenDane is not null && destinoDane is not null)
        {
            var origenSinCeros = origenDane.TrimStart('0');
            var destinoSinCeros = destinoDane.TrimStart('0');
            baseQ = dw.RutasSicetac.AsNoTracking().Where(r =>
                r.Periodo == periodo && r.CondicionCarga == condicionCarga &&
                r.Origen == origenSinCeros && r.Destino == destinoSinCeros);
            criterio = $"periodo {periodo} · {condicionCarga} · ruta DANE {origenDane}-{destinoDane}";
            claveRuta = $"dane:{origenSinCeros}:{destinoSinCeros}";
        }
        else
        {
            baseQ = dw.RutasSicetac.AsNoTracking().Where(r =>
                r.Periodo == periodo &&
                r.CondicionCarga == condicionCarga &&
                EF.Functions.Like(r.NombreRuta, $"%{origen}%-%{destino}%"));
            criterio = $"periodo {periodo} · {condicionCarga} · ruta ~ '{origen} - {destino}'";
            claveRuta = $"txt:{origen}:{destino}";
        }
        var claveBase = $"sice-filas:{periodo}:{condicionCarga}:{claveRuta}";

        clsRutaSicetacDw? fila = null;
        if (!string.IsNullOrWhiteSpace(vehiculo))
        {
            var config = vehiculo.Trim().ToUpper();
            fila = SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:{config}", baseQ.Where(r => r.Configuracion == config), ct), tipologia, carroceria);
            if (fila is not null) criterio += $" · configuración {config}";
        }
        fila ??= SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:*", baseQ, ct), tipologia, carroceria);
        return (fila, criterio);
    }

    internal static clsRutaSicetacDw? SeleccionarSice(
        List<clsRutaSicetacDw> filas, TipologiaCarga? tipologia, string? carroceria)
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

    private async Task<List<clsRutaSicetacDw>> FilasSiceCacheadasAsync(string clave, IQueryable<clsRutaSicetacDw> q, CancellationToken ct) =>
        (await _cache.GetOrCreateAsync(clave, async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await q.ToListAsync(ct);
        }))!;

    private async Task<int> PeriodoSiceAsync(clsDwSiceDbContext dw, CancellationToken ct)
    {
        var periodo = await _cache.GetOrCreateAsync("prop:sice-periodo", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await dw.RutasSicetac.Where(r => r.Periodo > 0).MaxAsync(r => (int?)r.Periodo, ct) ?? 0;
        });
        return periodo;
    }

    // --------------------------------------------- tbl_tarifas_plaza / histórico (propias)

    private sealed record clsCandidatoPropioRow(decimal Valor, DateOnly Vigencia, string DetallesTarifa, int? PesoId, string? TipoVehiculo);

    /// <summary>S5-HU-16.3/#256: candidato "plaza" contra nuestra propia tbl_tarifas_plaza
    /// (sembrada en S4-HU-10.1/10.2) — match EXACTO por Id de municipio, nunca por texto
    /// ni DANE. "Vigente" aquí es Vigencia &lt;= hoy (fecha desde la que aplica el precio;
    /// convención inversa a las tablas SAP externas, donde Vigencia es la fecha hasta la
    /// que el precio sigue siendo válido — confirmado contra datos reales: los 107 registros
    /// migrados en S4-HU-10.1 traen Vigencia = fecha de la migración, ya pasada).</summary>
    private Task<clsCandidatoTarifaDto?> CandidatoPlazaPropiaAsync(
        int origenMunicipioId, int destinoMunicipioId, string? vehiculo, decimal? peso, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(_clock.GetUtcNow().Date);
        var query = _db.TarifasPlaza.AsNoTracking()
            .Where(t => t.OrigenMunicipioId == origenMunicipioId && t.DestinoMunicipioId == destinoMunicipioId && t.Vigencia <= hoy)
            .Select(t => new clsCandidatoPropioRow(t.Valor, t.Vigencia, t.DetallesTarifa, t.PesoId, t.TipoVehiculo));
        return CandidatoPropioAsync(query, vehiculo, peso, "plaza", "tbl_tarifas_plaza", ct);
    }

    /// <summary>Mismo criterio que <see cref="CandidatoPlazaPropiaAsync"/>, contra
    /// tbl_tarifas_plaza_historico (la llena solo el trigger de BD, nunca la app).</summary>
    private Task<clsCandidatoTarifaDto?> CandidatoHistoricoPropioAsync(
        int origenMunicipioId, int destinoMunicipioId, string? vehiculo, decimal? peso, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(_clock.GetUtcNow().Date);
        var query = _db.TarifasPlazaHistorico.AsNoTracking()
            .Where(t => t.OrigenMunicipioId == origenMunicipioId && t.DestinoMunicipioId == destinoMunicipioId && t.Vigencia <= hoy)
            .Select(t => new clsCandidatoPropioRow(t.Valor, t.Vigencia, t.DetallesTarifa, t.PesoId, t.TipoVehiculo));
        return CandidatoPropioAsync(query, vehiculo, peso, "historico_sap", "tbl_tarifas_plaza_historico", ct);
    }

    /// <summary>
    /// Vehículo/peso son filtros TOLERANTES (igual que el fallback externo de siempre):
    /// solo se aplican si dejan al menos una fila — si no, se ignoran y se toma la de
    /// mayor valor entre todas las que ya cumplen municipio+vigencia. Materializa después
    /// del filtro por municipio+vigencia (tbl_tarifas_plaza/histórico son tablas chicas,
    /// ~100-300 filas): EF Core no traduce un Where posterior sobre una propiedad de
    /// <see cref="clsCandidatoPropioRow"/> ya proyectado (confirmado al verificar esta
    /// issue — "could not be translated"), así que el resto se filtra en memoria.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> CandidatoPropioAsync(
        IQueryable<clsCandidatoPropioRow> query, string? vehiculo, decimal? peso, string fuente, string tabla, CancellationToken ct)
    {
        var candidatos = await query.ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(vehiculo))
        {
            var conVehiculo = candidatos.Where(t => t.TipoVehiculo == vehiculo).ToList();
            if (conVehiculo.Count > 0) candidatos = conVehiculo;
        }

        if (peso is not null)
        {
            var pesoId = await _db.RangosPeso.AsNoTracking()
                .Where(r => r.PesoMinimo <= peso && (r.PesoMaximo == null || peso <= r.PesoMaximo))
                .Select(r => (int?)r.Id).FirstOrDefaultAsync(ct);
            if (pesoId is not null)
            {
                var conPeso = candidatos.Where(t => t.PesoId == pesoId).ToList();
                if (conPeso.Count > 0) candidatos = conPeso;
            }
        }

        var top = candidatos.OrderByDescending(t => t.Valor).FirstOrDefault();
        if (top is null || top.Valor <= 0) return null;

        return new clsCandidatoTarifaDto(
            fuente, top.Valor,
            $"vigencia {top.Vigencia:yyyy-MM-dd}" + (top.DetallesTarifa is { Length: > 0 } ? $" · {top.DetallesTarifa}" : ""),
            $"{tabla} (propia) · municipios", 1);
    }

    // ------------------------------------------------------------- SAP (plaza e histórico)

    /// <summary>Códigos de zona SAP (ZC_/ZTC_) de un municipio ya resuelto — ver
    /// clsMunicipio.NombreTarifasPlaza/ZonaTarificacion. Vacío (nunca matchea nada) si el
    /// municipio es null o no tiene ninguno de los dos códigos poblados.</summary>
    private static IReadOnlyList<string> CodigosZonaSap(clsMunicipio? municipio) =>
        new[] { municipio?.NombreTarifasPlaza, municipio?.ZonaTarificacion }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();

    /// <summary>
    /// S5-HU-16.3/#256: Origen/Destino de tblSAPTarifas/tblSAPTarifasLog guardan código
    /// DIVIPOLA o zona SAP (ZC_/ZTC_) — <see cref="Nexo.Infrastructure.External.clsSapTarifaBase"/>.
    /// Antes se resolvía un código DANE (tblSAPDANE) y, si no servía, cualquier fila cuyo
    /// Origen/Destino *contuviera* ese texto como substring (LIKE) — ninguno de los dos es
    /// el criterio real. Ahora compara EXACTO contra los códigos ZC_/ZTC_ ya cruzados en
    /// tbl_municipios (seeder S4-HU-09.1). Sin municipio resuelto para alguno de los dos
    /// lados (#254), o sin ningún código de zona poblado, no hay match posible — se
    /// devuelve null (la fuente no aplica), nunca se adivina.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> CandidatoSapAsync<T>(
        clsDbSolucionesTiDbContext dc, IQueryable<T> tabla, string fuente,
        clsMunicipio? origenMunicipio, clsMunicipio? destinoMunicipio,
        string? vehiculo, decimal? peso, bool soloVigentes,
        IReadOnlyDictionary<int, DateTime> ajustesVigencia, CancellationToken ct,
        int? ventanaMeses = null) where T : clsSapTarifaBase
    {
        var codigosOrigen = CodigosZonaSap(origenMunicipio);
        var codigosDestino = CodigosZonaSap(destinoMunicipio);
        if (codigosOrigen.Count == 0 || codigosDestino.Count == 0) return null;

        var query = tabla.Where(t => codigosOrigen.Contains(t.Origen) && codigosDestino.Contains(t.Destino));

        DateTime Efectiva(T t) => ajustesVigencia.TryGetValue(t.Id, out var v) ? v : t.Vigencia;

        var hoy = _clock.GetUtcNow().Date;
        var criterio = $"origen zona ({string.Join(",", codigosOrigen)}) · destino zona ({string.Join(",", codigosDestino)})";
        if (soloVigentes)
        {
            var idsAjustados = ajustesVigencia.Keys.ToList();
            query = query.Where(t => t.Vigencia >= hoy || idsAjustados.Contains(t.Id));
            criterio += " · vigencia efectiva >= hoy";
        }

        if (ventanaMeses is { } meses)
        {
            var desde = hoy.AddMonths(-meses);
            query = query.Where(t => t.FechaCreacion >= desde);
            criterio += $" · últimos {meses} meses";
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
        var zonaClave = $"{string.Join(',', codigosOrigen)}>{string.Join(',', codigosDestino)}";
        var claveFetch = $"sap-filas:{fuente}:{zonaClave}:{vehiculo}:{soloVigentes}:{hoy:yyyyMMdd}:{ajustesClave}:{ventanaMeses}";
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

        if (filas is List<clsSapTarifaLog> log)
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
        return new clsCandidatoTarifaDto(
            fuente, top.Valor!.Value,
            $"{top.Fila.Origen} → {top.Fila.Destino} · vigencia {vigenciaEfectiva:yyyy-MM-dd}{ajustada}" +
                (top.Fila is clsSapTarifaPlaza { DetallesTarifa: { Length: > 0 } det } ? $" · {det}" : ""),
            criterio, candidatas.Count);
    }

    private async Task<string?> IdVehiculoSapAsync(clsDbSolucionesTiDbContext dc, string codigoVehiculo, CancellationToken ct)
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

    private static decimal? ParseValor(string valor) => clsImporteParser.Parse(valor);

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
