using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
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
    public async Task<clsDecisionDeFlujo> ProponerParaLineaAsync(Guid lineaId, CancellationToken ct = default)
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
        var plazaInterna = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();
        var historicoInterno = scope.ServiceProvider.GetRequiredService<IHistoricoPlazaInternoPort>();
        var lineasNegocio = scope.ServiceProvider.GetRequiredService<ILineasNegocioPort>();
        var clasesViaje = scope.ServiceProvider.GetRequiredService<IClasesViajePort>();

        var (dto, modo) = await ProponerLineaAsync(
            _db, dw, dc, tarifario, plazaInterna, historicoInterno, lineasNegocio, clasesViaje,
            linea, ajustes, soloSice, horasSolicitud, ct);
        await PublicarVencidaSiAplicaAsync(dto, ct);
        return new clsDecisionDeFlujo(
            new clsResultadoDeCosteo(dto.ValorPropuesto, dto.FuentePropuesta, dto.Justificacion), modo, dto.MotivoPendiente);
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
                // S5-HU-18/#262: DbContext propio por tarea en paralelo — antes
                // ProponerLineaAsync usaba `_db` (el de la instancia, compartido) para
                // tbl_tarifas_plaza/histórico/rangos de peso, mientras que dw/dc sí tenían
                // scope propio. EF Core no es thread-safe: con varios grupos corriendo a
                // la vez, eso producía InvalidOperationException intermitentes ("A second
                // operation was started on this context instance before a previous
                // operation completed").
                var dbGrupo = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
                var dw = _dwConfigurado ? scope.ServiceProvider.GetService<clsDwSiceDbContext>() : null;
                var dc = _dcConfigurado ? scope.ServiceProvider.GetService<clsDbSolucionesTiDbContext>() : null;
                var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();
                var plazaInterna = scope.ServiceProvider.GetRequiredService<ITarifaPlazaInternaPort>();
                var historicoInterno = scope.ServiceProvider.GetRequiredService<IHistoricoPlazaInternoPort>();
                var lineasNegocio = scope.ServiceProvider.GetRequiredService<ILineasNegocioPort>();
                var clasesViaje = scope.ServiceProvider.GetRequiredService<IClasesViajePort>();
                var horasGrupo = HorasEfectivasDeLinea(grupo.First(), horasSolicitud);
                var (dto, _) = await ProponerLineaAsync(
                    dbGrupo, dw, dc, tarifario, plazaInterna, historicoInterno, lineasNegocio, clasesViaje,
                    grupo.First(), ajustes, soloSice, horasGrupo, ct);
                return grupo.Select(l => dto with { LineaId = l.Id }).ToList();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Regla inviolable 6: un grupo que falla (fuente caída, timeout) NO puede
                // llevarse los demás. Task.WhenAll falla rápido y descarta hasta los grupos
                // que ya habían terminado bien si uno solo lanza — por eso el catch va AQUÍ,
                // por grupo, y no alrededor del WhenAll.
                //
                // S5-HU-18/#262: ya NO se interpola ex.Message en la justificación — antes
                // un fallo técnico (ej. la carrera de DbContext de arriba, o cualquier otra
                // excepción de infraestructura) se mostraba crudo en la Mesa de Costeo
                // ("A second operation was started on this context instance..."). Es un
                // mensaje de negocio fijo; el detalle técnico queda solo para quien revise
                // logs del servidor, no para el usuario.
                var primera = grupo.First();
                const string justificacion = "No se pudo consultar la Biblioteca para esta línea. Intente de nuevo o revise manualmente.";
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

    private async Task<(clsPropuestaLineaDto Dto, ModoDecision Modo)> ProponerLineaAsync(
        clsNexoDbContext db, clsDwSiceDbContext? dw, clsDbSolucionesTiDbContext? dc, ITarifarioSapPort tarifario,
        ITarifaPlazaInternaPort plazaInterna, IHistoricoPlazaInternoPort historicoInterno,
        ILineasNegocioPort lineasNegocio, IClasesViajePort clasesViaje,
        clsLineaSolicitud linea, IReadOnlyDictionary<int, DateTime> ajustesVigencia,
        IReadOnlySet<string> soloSice, decimal horasSolicitud, CancellationToken ct)
    {
        if (linea.clsRuta is null)
        {
            return (new clsPropuestaLineaDto(
                linea.Id, null, null, linea.Peso, null, null, null, null, null,
                "La línea no tiene ruta definida: no es posible buscar en la Biblioteca."), ModoDecision.ConsultaCompleta);
        }

        var origen = Normalizar(linea.clsRuta.Origen);
        var destino = Normalizar(linea.clsRuta.Destino);
        var vehiculo = linea.clsConfiguracionVehiculo?.TipoVehiculo;

        // S5-HU-20.5/#297: Cupo (o sin clase de viaje) sin vehículo nunca se autocostea — ver
        // clsReglaDeCosteo.DecidirFlujo. Se calcula una sola vez y gobierna Solo-SICETAC, plaza que
        // gana sola y el atajo del histórico.
        var sinVehiculoQueLoExige = clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta(linea.ClaseViaje, vehiculo);

        // S5-HU-16.1/#254 + S5-HU-16.2/#255: código DANE real del municipio (tbl_municipios),
        // cuando la ruta ya resolvió contra el buscador de ciudades — null en rutas viejas
        // o con texto que no matcheó ningún municipio (ver clsResolucionDeCatalogos).
        var origenDane = linea.clsRuta.OrigenMunicipio?.CodigoDane;
        var destinoDane = linea.clsRuta.DestinoMunicipio?.CodigoDane;

        // S5-HU-19.2/#282: "hay regla" ahora es solo una BANDERA que entra a DecidirFlujo —
        // ya no corta temprano por su cuenta. Clave por DANE (tbl_reglas_solo_sicetac.Origen/
        // Destino SIEMPRE están en código DANE — carga masiva S4-HU-09.2): antes comparaba
        // contra el nombre de ciudad en texto libre de la ruta y nunca coincidía (#255).
        var hayReglaSoloSicetac = clsReglaSoloSicetacDeLinea.Tiene(soloSice, linea.ClaseViaje, origenDane, destinoDane, vehiculo);

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

        // clsTarifarioSapAdapter.SicetacOficialAsync (réplica 1:1 de la lógica SAP
        // legada) exige match exacto de Configuracion/TipoCarga/UnidadTransporte/
        // ViaEstandar sin fallback, y solo aplica a Contenedor/Cupo (nunca a
        // CargaSuelta/Extradimensionado) - eso deja sin piso automático rutas que sí
        // existen y son encontrables (reproducido con Medellín-Guarne), obligando a
        // marcar "Solo SICE-TAC" a mano para que aparezcan (esa marca usa
        // CandidatoSicetacAsync, el selector tolerante de abajo). Si el camino oficial
        // no encuentra nada, se reintenta con ESE MISMO selector antes de rendirse.
        //
        // S5-HU-19.2/#282: movido ANTES de plaza/histórico a propósito — en modo
        // SoloSicetac (regla + piso encontrado) ninguno de los dos hace falta, así que se
        // corta acá sin gastar esas consultas.
        var sicetac = oficial?.SicetacOficial;
        if (sicetac is null && dw is not null)
            sicetac = await CandidatoSicetacAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, linea.Tipologia, linea.clsConfiguracionVehiculo?.Carroceria, horasSolicitud, ct);

        // #300: si no hay piso SICE-TAC porque el período más reciente cargado no es el del mes actual, se dice
        // (justificación + motivo de pendiente) en vez de un "sin piso" mudo. Sin piso nunca hay modo automático
        // (DecidirFlujo exige sicetac para SoloSicetac y PlazaVigenteMayor): la línea queda para validación humana.
        string? motivoSice = null;
        if (sicetac is null && dw is not null)
            motivoSice = clsPeriodoSicetac.MotivoNoVigente(await PeriodoSiceAsync(dw, ct), _clock.GetUtcNow());

        if (hayReglaSoloSicetac && sicetac is not null)
        {
            var justifSolo = $"clsRuta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({sicetac.Valor:C0}) sin comparar con plaza ni histórico.";
            return (new clsPropuestaLineaDto(
                linea.Id, $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}", vehiculo, linea.Peso,
                sicetac, null, null, sicetac.Valor, "sicetac", justifSolo,
                ModoDecision: nameof(ModoDecision.SoloSicetac)), ModoDecision.SoloSicetac);
        }

        // S5-HU-19.1/#281: nombre del catálogo externo (NO el enum, NO un Id) que guardan
        // tbl_tarifas_plaza/tbl_tarifas_plaza_historico — ver clsTarifaPlaza, doc-comment.
        var nombreLineaNegocio = await NombreLineaNegocioAsync(lineasNegocio, linea.LineaNegocio, ct);
        var nombreClaseViaje = await NombreClaseViajeAsync(clasesViaje, linea.ClaseViaje, ct);
        var consultaInterna = new clsConsultaTarifaPlazaInterna(
            linea.clsRuta.Origen, linea.clsRuta.Destino, nombreLineaNegocio, nombreClaseViaje,
            linea.ClaseContenedor, vehiculo, linea.Peso,
            linea.clsRuta.OrigenMunicipioId, linea.clsRuta.DestinoMunicipioId);

        // El camino "oficial" (ITarifarioSapPort, ver comentario mas abajo) exige match
        // exacto de ClaseContenedor/peso/vehiculo sin fallback. Cuando no encuentra nada
        // (oficial es null, o oficial.Tarifa queda null porque el filtro exacto descarto
        // todo) se reintenta con el mismo selector tolerante que ya usa el camino "Solo
        // SICE-TAC" de arriba, antes de rendirse. Mismo patron que ya se aplico para el
        // piso SICE-TAC (ver sicetac mas abajo) y para el bug gemelo documentado en
        // docs/decisiones/matching-sicetac-oficial-vs-solo-sicetac.md.
        //
        // S5-HU-19.1/#281: orden de fuentes de PLAZA = oficial SAP -> tolerante SAP (por
        // zona) -> propia de NEXO (tbl_tarifas_plaza), ganando la primera VIGENTE. Antes
        // de esta issue, "oficial" trayendo una VENCIDA ya dejaba `plaza` no-null y las
        // otras dos fuentes nunca se intentaban — un bug real: una plaza vigente en SAP
        // tolerante o en la tabla propia nunca se encontraba si oficial traía algo vencido
        // primero. Ahora se sigue buscando una vigente en las tres fuentes; la vencida
        // (si la hay) solo queda como referencia cuando NINGUNA fuente trae una vigente.
        clsCandidatoTarifaDto? plazaVigente = oficial?.Vencida == false ? oficial.Tarifa : null;
        clsCandidatoTarifaDto? plazaVencidaRef = oficial?.Vencida == true ? oficial.Tarifa : null;

        if (plazaVigente is null && dc is not null)
            plazaVigente = await CandidatoSapAsync(
                dc, dc.TarifasPlazaSap, "plaza", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct);

        if (plazaVigente is null)
        {
            var resultadoPlazaInterna = await plazaInterna.BuscarAsync(consultaInterna, ct);
            plazaVigente = resultadoPlazaInterna.Vigente;
            plazaVencidaRef ??= resultadoPlazaInterna.Vencida;
        }

        // S5-HU-19.2/#282, Paso 2 anticipado: si la plaza vigente ya va a ganar sola
        // (PlazaVigenteMayor), el histórico ni se consulta — DecidirFlujo no lo mira en
        // ese modo, pedirlo sería una consulta tirada a la basura.
        var seriaPlazaVigenteMayor = !sinVehiculoQueLoExige && !hayReglaSoloSicetac && sicetac is not null &&
            plazaVigente is not null && plazaVigente.Valor > sicetac.Valor;

        clsCandidatoTarifaDto? historico = null;
        if (!seriaPlazaVigenteMayor)
        {
            // S5-HU-19.1/#281: mismo orden que plaza — log externo de SAP primero
            // (ventana de 3 meses), y solo si no hay nada ahí, el histórico propio de NEXO.
            if (dc is not null)
                historico = await CandidatoSapAsync(
                    dc, dc.TarifasLogSap, "historico_sap", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                    vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct, ventanaMeses: 3);
            if (historico is null)
                historico = await historicoInterno.BuscarAsync(consultaInterna, ct);
        }

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
                plazaVigente = enVivo;
                plazaVencidaRef = null;
            }
        }

        var decision = clsReglaDeCosteo.DecidirFlujo(
            hayReglaSoloSicetac, ADominio(sicetac), ADominio(plazaVigente), ADominio(plazaVencidaRef), ADominio(historico),
            lineaSinVehiculoQueLoExige: sinVehiculoQueLoExige);
        var (valor, fuente, justificacion) = (decision.Resultado.Valor, decision.Resultado.Fuente, decision.Resultado.Justificacion);
        var plaza = plazaVigente ?? plazaVencidaRef;
        var vencida = plazaVigente is null && plazaVencidaRef is not null;

        // Cuando la propuesta final es plaza y ese candidato viene del fallback
        // conservador (no un match exacto por vehículo/peso), la advertencia se anexa
        // a la MISMA justificación que ya se le muestra al usuario - no hace falta que
        // el frontend sepa leer un campo aparte para enterarse.
        if (fuente == "plaza" && plaza?.Advertencia is { } advertenciaPlaza)
            justificacion += $" ⚠ {advertenciaPlaza}";

        // #300: el retorno del contenedor es un tramo VACÍO: piso SICE-TAC de viaje vacío (destino -> ciudad de
        // devolución) con 1h+1h, no la tarifa de plaza de la línea 6. Mismo vehículo que el viaje cargado.
        clsCandidatoTarifaDto? retorno = null;
        if (linea.RetornaContenedor && !string.IsNullOrWhiteSpace(linea.CiudadDevolucionContenedor) && dw is not null)
            retorno = await RetornoContenedorAsync(dw, destino, Normalizar(linea.CiudadDevolucionContenedor), vehiculo ?? sicetac?.Configuracion, ct);

        if (retorno is not null)
            justificacion += $" Retorno del contenedor a {linea.CiudadDevolucionContenedor}: {retorno.Valor:C0} (viaje vacío SICE-TAC, {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h).";
        else if (linea.RetornaContenedor)
            justificacion += " El viaje retorna contenedor pero no se encontró piso SICE-TAC de viaje vacío para el retorno.";

        var motivoPendiente = decision.MotivoPendiente;
        if (motivoSice is not null)
        {
            justificacion = $"{motivoSice} {justificacion}";
            motivoPendiente ??= $"{motivoSice} El Administrador de Tarifas debe validar el flete.";
        }

        return (new clsPropuestaLineaDto(
            linea.Id,
            $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}",
            vehiculo,
            linea.Peso,
            sicetac, plaza, historico,
            valor, fuente, justificacion,
            retorno,
            Vencida: vencida,
            ModoDecision: decision.Modo.ToString(),
            MotivoPendiente: motivoPendiente), decision.Modo);
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
        // una línea, sin clsRuta/municipio resuelto): sigue comparando por texto (sin DANE disponible).
        // #300: es un tramo VACÍO — 1h+1h (2 h en total, antes null = las pactadas de DWSice) y el piso se
        // recalcula con clsPisoConHoras, igual que el retorno del contenedor.
        return await CandidatoSicetacAsync(
            dw, Normalizar(ciudadLevante), Normalizar(origen), origenDane: null, destinoDane: null, configuracion,
            "VACIO", tipologia: null, carroceria: null, clsResolucionHorasLinea.HorasTotalesTramoVacio, ct);
    }

    /// <summary>
    /// #300: retorno del contenedor vacío = piso SICE-TAC de viaje VACÍO desde el destino de la línea hasta la ciudad
    /// de devolución, con 1h+1h — en vez de la tarifa de plaza de la línea 6 (que se retiró de este cálculo). El
    /// viaje cargado de la misma línea conserva sus propias horas (4+4 por defecto). Null si no hay piso para la ruta.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> RetornoContenedorAsync(
        clsDwSiceDbContext dw, string origenRetorno, string destinoRetorno, string? configuracion, CancellationToken ct)
    {
        var c = await CandidatoSicetacAsync(
            dw, origenRetorno, destinoRetorno, origenDane: null, destinoDane: null, configuracion,
            "VACIO", tipologia: null, carroceria: null, clsResolucionHorasLinea.HorasTotalesTramoVacio, ct);
        return c is null ? null : c with
        {
            Fuente = "retorno_contenedor",
            Criterio = $"retorno de contenedor · viaje vacío · {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h · {c.Criterio}",
        };
    }

    /// <summary>#300: motivo visible cuando el piso SICE-TAC no se usa por período (ver
    /// <see cref="IPropuestaTarifaPort.MotivoSicetacNoUsadoAsync"/>). Null si DWSice no está configurado.</summary>
    public async Task<string?> MotivoSicetacNoUsadoAsync(CancellationToken ct = default)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<clsDwSiceDbContext>();
        return dw is null ? null : clsPeriodoSicetac.MotivoNoVigente(await PeriodoSiceAsync(dw, ct), _clock.GetUtcNow());
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
            criterio, 1,
            ValorPisoBase: fila.Valor, ValorTiemposLogisticos: valor - fila.Valor, Configuracion: fila.Configuracion);
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
        // #300: solo se usa el período del mes y año actuales (sin mes de gracia): si el más reciente cargado no
        // lo es, no hay piso. El motivo visible sale de MotivoSicetacNoUsadoAsync / ProponerLineaAsync.
        var periodo = await PeriodoSiceAsync(dw, ct);
        if (!clsPeriodoSicetac.EsElDelMesActual(periodo, _clock.GetUtcNow())) return (null, string.Empty);

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

    /// <summary>
    /// Período AAAAMM más reciente cargado en DWSice HASTA el mes actual (0 si no hay ninguno). Un período futuro ya
    /// cargado no tapa al vigente. La clave de caché lleva el período actual: al cambiar de mes no se reusa el
    /// valor del mes anterior. Quien lo usa decide si es el vigente (<see cref="clsPeriodoSicetac"/>).
    /// </summary>
    private async Task<int> PeriodoSiceAsync(clsDwSiceDbContext dw, CancellationToken ct)
    {
        var actual = clsPeriodoSicetac.Actual(_clock.GetUtcNow());
        return await _cache.GetOrCreateAsync($"prop:sice-periodo:{actual}", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await dw.RutasSicetac.Where(r => r.Periodo > 0 && r.Periodo <= actual).MaxAsync(r => (int?)r.Periodo, ct) ?? 0;
        });
    }

    // --------------------------------------------- catálogo externo: nombre por enum

    /// <summary>S5-HU-19.1/#281: resuelve el NOMBRE (no el Id, no el enum) que
    /// tbl_tarifas_plaza/tbl_tarifas_plaza_historico guardan para línea de negocio — vía
    /// el mismo mapeo enum-&gt;Id SAP que ya usa clsTarifarioSapAdapter para construir su
    /// propio filtro (CodigoLinea), aplicado sobre el catálogo real (ILineasNegocioPort)
    /// en vez de comparar contra un Id de otra base de datos.</summary>
    private static async Task<string?> NombreLineaNegocioAsync(ILineasNegocioPort port, LineaNegocio? valor, CancellationToken ct)
    {
        if (valor is not { } ln || !clsTarifarioSapAdapter.CodigoLinea.TryGetValue(ln, out var id)) return null;
        var catalogo = await port.ListarAsync(ct);
        return catalogo.FirstOrDefault(x => x.Id == id)?.Descripcion;
    }

    private static async Task<string?> NombreClaseViajeAsync(IClasesViajePort port, ClaseViaje? valor, CancellationToken ct)
    {
        if (valor is not { } cv || !clsTarifarioSapAdapter.CodigoClaseViaje.TryGetValue(cv, out var id)) return null;
        var catalogo = await port.ListarAsync(ct);
        return catalogo.FirstOrDefault(x => x.Id == id)?.Descripcion;
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
