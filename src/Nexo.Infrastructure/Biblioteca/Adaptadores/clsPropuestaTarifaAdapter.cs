using System.Globalization;
using System.Text;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza.Interfaces;
using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
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
/// Propone por línea el valor más alto entre el piso SICE-TAC (DWSice), las tarifas de plaza SAP y el histórico SAP.
/// Los cargues masivos se procesan en paralelo con un scope de DbContext por trabajador (los contextos no son thread-safe).
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
    private readonly IMapeoVehiculoTarifarioPort _mapeo;
    private readonly IRangosPesoContenedorPort _rangos;
    private readonly bool _dwConfigurado;
    private readonly bool _dcConfigurado;

    public clsPropuestaTarifaAdapter(
        clsNexoDbContext db, IServiceProvider sp, IServiceScopeFactory scopes,
        IMemoryCache cache, TimeProvider clock, IAjustesVigenciaPlazaRepository ajustes,
        IClavesSoloSicetacPort reglas, ISapTarifaFleteEnVivoPort sapEnVivo, IPublicadorDeMensajes publicador,
        IMapeoVehiculoTarifarioPort mapeo, IRangosPesoContenedorPort rangos)
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
        _mapeo = mapeo;
        _rangos = rangos;
    }

    /// <summary>
    /// Propuesta de una sola línea, usada por el auto-costeo asíncrono: una excepción en una fuente queda contenida en esa línea en vez de llevarse las 300 (regla inviolable 6).
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

        // Las horas ya resueltas de la propia línea tienen prioridad sobre las de la solicitud completa; si la línea no las tiene, se usa la de la solicitud.
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
            new clsResultadoDeCosteo(dto.ValorPropuesto, dto.FuentePropuesta, dto.Justificacion), modo, dto.MotivoPendiente,
            dto.AprovisionamientoContenedor?.Valor, dto.RetornoContenedor?.Valor);
    }

    /// <summary>
    /// Alerta de urgencia Alta cuando el costeo descarta una tarifa de plaza por vencida (RN-02). Solo desde el camino por línea: el camino por solicitud corre grupos
    /// en paralelo sobre el mismo DbContext donde escribe el publicador (carrera de datos). Deduplicada por día.
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

        // Horas reales capturadas (Cargue+Descargue) para recalcular el piso SICE-TAC en vez de asumir las pactadas; HorasEfectivasDeLinea prefiere las de la línea si ya están resueltas.
        var tiempos = clsCondicionesServicioDto.Desde(solicitud.CondicionesServicio).TiemposLogisticos!;
        var horasSolicitud = tiempos.HorasCargue + tiempos.HorasDescargue;

        // Horas entra en la huella: dos líneas idénticas salvo por las horas pueden costear distinto (clsPisoConHoras.Calcular) y agruparlas daría a ambas el piso de una sola.
        string Huella(clsLineaSolicitud l) => string.Join('|',
            l.clsRuta?.Origen, l.clsRuta?.Destino, l.clsConfiguracionVehiculo?.TipoVehiculo, l.clsConfiguracionVehiculo?.Carroceria,
            l.Tipologia, l.Peso, l.LineaNegocio, l.ClaseViaje, l.ClaseContenedor,
            l.Exclusivo, l.Consolidado, l.RetornaContenedor, l.CiudadDevolucionContenedor,
            l.LevanteContenedorDiferente, l.CiudadLevanteContenedor,
            HorasEfectivasDeLinea(l, horasSolicitud));

        var grupos = lineas.GroupBy(Huella).ToList();

        using var sem = new SemaphoreSlim(Concurrencia);
        var tareas = grupos.Select(async grupo =>
        {
            await sem.WaitAsync(ct);
            try
            {
                using var scope = _scopes.CreateScope();
                // DbContext propio por tarea en paralelo: EF Core no es thread-safe y compartir `_db` entre grupos produce InvalidOperationException intermitentes ("A second operation was started...").
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
                // Regla inviolable 6: un grupo que falla (fuente caída, timeout) no puede llevarse los demás. Task.WhenAll falla rápido y descarta incluso los grupos ya terminados,
                // por eso el catch va aquí, por grupo, y no alrededor del WhenAll.
                // La justificación es un mensaje de negocio fijo: no se interpola ex.Message para no mostrar detalles técnicos al usuario; el detalle queda en los logs.
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

    /// <summary>Prefiere las horas ya resueltas de la línea (clsResolucionHorasLinea.Resolver) sobre el valor único de la solicitud; si la línea no las tiene, cae al de la solicitud.</summary>
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
        var carroceriaLinea = linea.clsConfiguracionVehiculo?.Carroceria;
        // Una línea de devolución de contenedor se costea como viaje VACÍO y no tiene retorno ni levante propios.
        var esDevolucion = linea.LineaNegocio == LineaNegocio.DevolucionContenedor;
        var esContenedor = esDevolucion || linea.ClaseViaje == ClaseViaje.Contenedor;

        // Cupo (o sin clase de viaje) sin vehículo nunca se autocostea (clsReglaDeCosteo.DecidirFlujo). Se calcula una vez y gobierna Solo-SICETAC, la plaza que gana sola y el atajo del histórico.
        var sinVehiculoQueLoExige = clsReglaDeCosteo.LineaSinVehiculoQueExigeConsultaCompleta(linea.ClaseViaje, vehiculo);

        // Código DANE real del municipio (tbl_municipios) cuando la ruta ya resolvió contra el buscador de ciudades; null si el texto no matcheó ninguno (ver clsResolucionDeCatalogos).
        var origenDane = linea.clsRuta.OrigenMunicipio?.CodigoDane;
        var destinoDane = linea.clsRuta.DestinoMunicipio?.CodigoDane;

        // "Hay regla" es solo una bandera que entra a DecidirFlujo. Clave por DANE: tbl_reglas_solo_sicetac.Origen/Destino siempre están en código DANE, no en nombre de ciudad.
        var hayReglaSoloSicetac = clsReglaSoloSicetacDeLinea.Tiene(soloSice, linea.ClaseViaje, origenDane, destinoDane, vehiculo);

        clsResultadoTarifarioSap? oficial = null;
        if (linea.ClaseViaje is not null && dc is not null)
        {
            oficial = await tarifario.ConsultarAsync(new clsConsultaTarifarioSap(
                linea.clsRuta.Origen, linea.clsRuta.Destino,
                linea.LineaNegocio, linea.ClaseViaje, linea.ClaseContenedor,
                vehiculo, linea.clsConfiguracionVehiculo?.Carroceria,
                linea.Peso, linea.Exclusivo, linea.Consolidado,
                linea.RetornaContenedor, linea.CiudadDevolucionContenedor, horasSolicitud, origenDane, destinoDane), ct);
        }

        // clsTarifarioSapAdapter.SicetacOficialAsync exige match exacto de Configuracion/TipoCarga/UnidadTransporte/ViaEstandar sin fallback y solo aplica a Contenedor/Cupo,
        // lo que dejaría sin piso automático rutas existentes (p. ej. Medellín-Guarne). Si el camino oficial no encuentra nada se reintenta con el selector tolerante
        // de CandidatoSicetacAsync antes de rendirse. Va antes de plaza/histórico: en modo SoloSicetac (regla + piso) ninguno de los dos hace falta.
        // Devolución de contenedor: el piso es el del viaje VACÍO (con las horas de la línea, 1 h + 1 h por defecto), no el cargado.
        var sicetac = esDevolucion ? null : oficial?.SicetacOficial;
        if (sicetac is null && dw is not null)
            sicetac = esDevolucion
                ? await CandidatoVacioAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, carroceriaLinea, esContenedor: true,
                    permitirVehiculoRelajado: true, horasSolicitud, ct)
                : await CandidatoSicetacAsync(
                    dw, origen, destino, origenDane, destinoDane,
                    await VehiculoDeContenedorSinVehiculoAsync(linea, vehiculo, esContenedor, ct) ?? vehiculo,
                    linea.Tipologia, carroceriaLinea ?? CarroceriaPorDefectoDeContenedor(esContenedor), horasSolicitud, ct);

        // Si no hay piso SICE-TAC porque el período más reciente no es el del mes actual, se dice (justificación + motivo de pendiente) en vez de un "sin piso" mudo.
        // Sin piso nunca hay modo automático (DecidirFlujo exige sicetac para SoloSicetac y PlazaVigenteMayor): la línea queda para validación humana.
        string? motivoSice = null;
        if (sicetac is null && dw is not null)
            motivoSice = clsPeriodoSicetac.MotivoNoVigente(await PeriodoSiceAsync(dw, ct), _clock.GetUtcNow());

        if (hayReglaSoloSicetac && sicetac is { VehiculoRelajado: false })
        {
            var justifSolo = $"La ruta marcada como SOLO SICE-TAC por el Administrador: se propone el piso SICE-TAC ({clsMoneda.Pesos(sicetac.Valor)}) sin comparar con plaza ni histórico.";
            var dtoSolo = new clsPropuestaLineaDto(
                linea.Id, $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}", vehiculo, linea.Peso,
                sicetac, null, null, sicetac.Valor, "sicetac", justifSolo,
                ModoDecision: nameof(ModoDecision.SoloSicetac));
            return (await ConTramosVaciosAsync(
                dtoSolo, linea, db, dw, origen, destino, origenDane, destinoDane, vehiculo ?? sicetac.Configuracion, carroceriaLinea,
                esDevolucion, esContenedor, ct), ModoDecision.SoloSicetac);
        }

        // Nombre del catálogo externo (no el enum ni un Id) que guardan tbl_tarifas_plaza/tbl_tarifas_plaza_historico.
        var nombreLineaNegocio = await NombreLineaNegocioAsync(lineasNegocio, linea.LineaNegocio, ct);
        var nombreClaseViaje = await NombreClaseViajeAsync(clasesViaje, linea.ClaseViaje, ct);
        var consultaInterna = new clsConsultaTarifaPlazaInterna(
            linea.clsRuta.Origen, linea.clsRuta.Destino, nombreLineaNegocio, nombreClaseViaje,
            linea.ClaseContenedor, vehiculo, linea.Peso,
            linea.clsRuta.OrigenMunicipioId, linea.clsRuta.DestinoMunicipioId,
            ClaseContenedorEstricta: esDevolucion);

        // El camino "oficial" (ITarifarioSapPort) exige match exacto de ClaseContenedor/peso/vehículo sin fallback; si no encuentra nada (oficial null o su Tarifa null)
        // se reintenta con el selector tolerante del camino "Solo SICE-TAC" antes de rendirse (ver docs/decisiones/matching-sicetac-oficial-vs-solo-sicetac.md).
        // Orden de fuentes de plaza: oficial SAP -> tolerante SAP (por zona) -> propia de NEXO (tbl_tarifas_plaza); gana la primera VIGENTE.
        // Se sigue buscando una vigente en las tres: una vencida de "oficial" no debe impedir hallarla en las otras, y solo queda como referencia si ninguna fuente trae una vigente.
        clsCandidatoTarifaDto? plazaVigente = oficial?.Vencida == false ? oficial.Tarifa : null;
        clsCandidatoTarifaDto? plazaVencidaRef = oficial?.Vencida == true ? oficial.Tarifa : null;

        if (plazaVigente is null && dc is not null)
        {
            IQueryable<clsSapTarifaPlaza> tablaPlaza = dc.TarifasPlazaSap;
            if (esDevolucion)
            {
                // Devolución de contenedor: una tarifa que no coincide en consolidado, exclusivo y clase de contenedor no cuenta.
                var consolidado = linea.Consolidado;
                var exclusivo = linea.Exclusivo;
                tablaPlaza = tablaPlaza.Where(t => (t.Consolidado ?? false) == consolidado && (t.Exclusivo ?? false) == exclusivo);
                if (!string.IsNullOrWhiteSpace(linea.ClaseContenedor))
                {
                    var claseId = await IdClaseContenedorSapAsync(dc, linea.ClaseContenedor, ct);
                    tablaPlaza = claseId is null ? tablaPlaza.Where(_ => false) : tablaPlaza.Where(t => t.ClaseContenedor == claseId);
                }
            }
            plazaVigente = await CandidatoSapAsync(
                dc, tablaPlaza, "plaza", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                vehiculo, linea.Peso, soloVigentes: true, ajustesVigencia, ct,
                exigeVehiculoDePlaza: linea.ClaseViaje == ClaseViaje.Cupo,
                criterioExtra: esDevolucion ? "devolución de contenedor: consolidado, exclusivo y clase de contenedor iguales a los de la línea" : null);
        }

        if (plazaVigente is null)
        {
            var resultadoPlazaInterna = await plazaInterna.BuscarAsync(consultaInterna, ct);
            plazaVigente = resultadoPlazaInterna.Vigente;
            plazaVencidaRef ??= resultadoPlazaInterna.Vencida;
        }

        // Si la plaza vigente ya va a ganar sola (PlazaVigenteMayor), el histórico ni se consulta: DecidirFlujo no lo mira en ese modo.
        var seriaPlazaVigenteMayor = !sinVehiculoQueLoExige && !hayReglaSoloSicetac && sicetac is { VehiculoRelajado: false } &&
            plazaVigente is { VehiculoRelajado: false } && plazaVigente.Valor > sicetac.Valor;

        clsCandidatoTarifaDto? historico = null;
        if (!seriaPlazaVigenteMayor)
        {
            // Mismo orden que plaza: log externo de SAP primero (ventana de 3 meses) y, si no hay nada, el histórico propio de NEXO.
            if (dc is not null)
                historico = await CandidatoSapAsync(
                    dc, dc.TarifasLogSap, "historico_sap", linea.clsRuta.OrigenMunicipio, linea.clsRuta.DestinoMunicipio,
                    vehiculo, linea.Peso, soloVigentes: false, ajustesVigencia, ct, ventanaMeses: 3,
                    exigeVehiculoDePlaza: linea.ClaseViaje == ClaseViaje.Cupo);
            if (historico is null)
                historico = await historicoInterno.BuscarAsync(consultaInterna, ct);
        }

        // Plaza en vivo (conexión SAP nueva): con SapTarifaFleteEnVivo:Modo en Deshabilitado o Real sin adaptador, _sapEnVivo.Configurado es false y esta rama no corre.
        // Solo sustituye la plaza (nunca el piso SICE-TAC ni el histórico), y "vencida" no aplica: la respuesta en vivo es siempre la vigente.
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

        // Si la propuesta final es plaza y el candidato viene del fallback conservador (no un match exacto por vehículo/peso), la advertencia se anexa a la misma justificación que ve el usuario.
        if (fuente == "plaza" && plaza?.Advertencia is { } advertenciaPlaza)
            justificacion += $" ⚠ {advertenciaPlaza}";

        var motivoPendiente = decision.MotivoPendiente;
        if (motivoSice is not null)
        {
            justificacion = $"{motivoSice} {justificacion}";
            motivoPendiente ??= $"{motivoSice} El Administrador de Tarifas debe validar el flete.";
        }

        var dto = new clsPropuestaLineaDto(
            linea.Id,
            $"{linea.clsRuta.Origen} → {linea.clsRuta.Destino}",
            vehiculo,
            linea.Peso,
            sicetac, plaza, historico,
            valor, fuente, justificacion,
            Vencida: vencida,
            ModoDecision: decision.Modo.ToString(),
            MotivoPendiente: motivoPendiente);

        // Retorno del contenedor y levante: tramos vacíos aparte del flete, con el mismo vehículo y carrocería de la línea.
        dto = await ConTramosVaciosAsync(
            dto, linea, db, dw, origen, destino, origenDane, destinoDane, vehiculo ?? sicetac?.Configuracion, carroceriaLinea,
            esDevolucion, esContenedor, ct);
        return (dto, decision.Modo);
    }

    // La decisión vive en Domain (clsReglaDeCosteo): son invariantes del negocio. Aquí solo se traen los candidatos y se traducen los DTO a objetos de valor del dominio.

    private static clsCandidatoDeTarifa? ADominio(clsCandidatoTarifaDto? c) =>
        c is null ? null : new clsCandidatoDeTarifa(c.Fuente, c.Valor, c.VehiculoRelajado);

    // ------------------------------------------------------------- SICE-TAC (DWSice)

    public async Task<clsCandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default, string? carroceria = null)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<clsDwSiceDbContext>();
        if (dw is null) return null;

        // Tramo vacío (1 h + 1 h) sin línea de solicitud detrás: las dos ciudades llegan escritas a mano; cada una se resuelve a su municipio y con los dos códigos DANE
        // la ruta se compara exacta (el texto no sirve: SICE-TAC escribe "BOGOTA D. C." y el catálogo "BOGOTA, D.C."). Si alguna no se resuelve a un único municipio, se busca por texto.
        var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
        var levanteResuelto = await ResolverDaneCiudadAsync(db, ciudadLevante, ct);
        var origenResuelto = await ResolverDaneCiudadAsync(db, origen, ct);
        var (levanteDane, origenDane) = levanteResuelto is not null && origenResuelto is not null
            ? (levanteResuelto, origenResuelto) : ((string?)null, (string?)null);

        // Con carrocería se elige la fila de esa unidad de transporte, igual que al costear una línea; sin carrocería no hay forma
        // de saber cuál corresponde y se devuelve una aproximación (la menor), avisándolo.
        if (!string.IsNullOrWhiteSpace(carroceria))
            return await AprovisionamientoContenedorAsync(
                dw, Normalizar(ciudadLevante), Normalizar(origen), levanteDane, origenDane, configuracion, carroceria,
                esContenedor: false, ct);

        var aproximado = await CandidatoSicetacAsync(
            dw, Normalizar(ciudadLevante), Normalizar(origen), levanteDane, origenDane, configuracion,
            "VACIO", tipologia: null, carroceria: null, clsResolucionHorasLinea.HorasTotalesTramoVacio, ct);
        return aproximado is null ? null : aproximado with
        {
            Advertencia = "Sin carrocería: valor aproximado (el menor entre las unidades de transporte de la ruta).",
        };
    }

    /// <summary>
    /// Retorno del contenedor vacío: piso SICE-TAC de viaje VACÍO del destino de la línea a la ciudad de devolución, con 1 h + 1 h y el mismo vehículo y carrocería.
    /// Null si no hay piso exacto para esa ruta.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> RetornoContenedorAsync(
        clsDwSiceDbContext dw, string origenRetorno, string destinoRetorno, string? origenDane, string? destinoDane,
        string? configuracion, string? carroceria, bool esContenedor, CancellationToken ct)
    {
        var c = await CandidatoVacioAsync(
            dw, origenRetorno, destinoRetorno, origenDane, destinoDane, configuracion, carroceria, esContenedor,
            permitirVehiculoRelajado: false, clsResolucionHorasLinea.HorasTotalesTramoVacio, ct);
        return c is null ? null : c with
        {
            Fuente = "retorno_contenedor",
            Criterio = $"retorno de contenedor · viaje vacío · {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h · {c.Criterio}",
        };
    }

    /// <summary>
    /// Levante del contenedor (aprovisionamiento): piso SICE-TAC de viaje VACÍO de la ciudad de levante al origen de la línea, con 1 h + 1 h y el mismo vehículo y carrocería.
    /// Null si no hay piso exacto para esa ruta.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> AprovisionamientoContenedorAsync(
        clsDwSiceDbContext dw, string ciudadLevante, string origenLinea, string? levanteDane, string? origenDane,
        string? configuracion, string? carroceria, bool esContenedor, CancellationToken ct)
    {
        var c = await CandidatoVacioAsync(
            dw, ciudadLevante, origenLinea, levanteDane, origenDane, configuracion, carroceria, esContenedor,
            permitirVehiculoRelajado: false, clsResolucionHorasLinea.HorasTotalesTramoVacio, ct);
        return c is null ? null : c with
        {
            Fuente = "aprovisionamiento_contenedor",
            Criterio = $"aprovisionamiento · viaje vacío · {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h · {c.Criterio}",
        };
    }

    /// <summary>
    /// Agrega los tramos vacíos que pide la línea (retorno y levante) en los tres caminos de decisión, por ser costos aparte del flete. Si pide un tramo y no hay piso exacto,
    /// la línea queda pendiente (<see cref="clsPropuestaLineaDto.MotivoPendiente"/>) y no se guarda sola. Una línea que ES la devolución no tiene retorno ni levante propios.
    /// </summary>
    private async Task<clsPropuestaLineaDto> ConTramosVaciosAsync(
        clsPropuestaLineaDto dto, clsLineaSolicitud linea, clsNexoDbContext db, clsDwSiceDbContext? dw, string origen, string destino,
        string? origenDane, string? destinoDane, string? vehiculo, string? carroceria, bool esDevolucion, bool esContenedor, CancellationToken ct)
    {
        if (esDevolucion) return dto;
        if (!linea.RetornaContenedor && !linea.LevanteContenedorDiferente) return dto;

        clsCandidatoTarifaDto? retorno = null;
        clsCandidatoTarifaDto? aprovisionamiento = null;
        var faltantes = new List<string>();
        var justificacion = dto.Justificacion;

        if (linea.RetornaContenedor)
        {
            var ciudad = linea.CiudadDevolucionContenedor;
            if (!string.IsNullOrWhiteSpace(ciudad) && dw is not null)
            {
                // El extremo de la línea ya trae su código DANE; el de la ciudad de devolución se resuelve por nombre. Con los dos
                // códigos la ruta se compara exacta; si la ciudad no se resuelve a un único municipio, por texto.
                var devolucionDane = await ResolverDaneCiudadAsync(db, ciudad, ct);
                var (dOrigen, dDestino) = destinoDane is not null && devolucionDane is not null ? (destinoDane, devolucionDane) : (null, null);
                retorno = await RetornoContenedorAsync(dw, destino, Normalizar(ciudad), dOrigen, dDestino, vehiculo, carroceria, esContenedor, ct);
            }
            if (retorno is not null)
                justificacion += $" Retorno del contenedor a {ciudad}: {clsMoneda.Pesos(retorno.Valor)} (viaje vacío SICE-TAC, {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h).";
            else
                faltantes.Add(string.IsNullOrWhiteSpace(ciudad) ? "retorno del contenedor (sin ciudad de devolución)" : $"retorno del contenedor a {ciudad}");
        }

        if (linea.LevanteContenedorDiferente)
        {
            var ciudad = linea.CiudadLevanteContenedor;
            if (!string.IsNullOrWhiteSpace(ciudad) && dw is not null)
            {
                var levanteDane = await ResolverDaneCiudadAsync(db, ciudad, ct);
                var (dOrigen, dDestino) = levanteDane is not null && origenDane is not null ? (levanteDane, origenDane) : (null, null);
                aprovisionamiento = await AprovisionamientoContenedorAsync(dw, Normalizar(ciudad), origen, dOrigen, dDestino, vehiculo, carroceria, esContenedor, ct);
            }
            if (aprovisionamiento is not null)
                justificacion += $" Aprovisionamiento desde {ciudad}: {clsMoneda.Pesos(aprovisionamiento.Valor)} (viaje vacío SICE-TAC, {clsResolucionHorasLinea.HorasCargueTramoVacio:0.##} h + {clsResolucionHorasLinea.HorasDescargueTramoVacio:0.##} h).";
            else
                faltantes.Add(string.IsNullOrWhiteSpace(ciudad) ? "levante del contenedor (sin ciudad de levante)" : $"levante del contenedor desde {ciudad}");
        }

        var motivo = dto.MotivoPendiente;
        if (faltantes.Count > 0)
        {
            var texto = $"No se encontró piso SICE-TAC de viaje vacío para el {string.Join(" ni para el ", faltantes)} (mismo vehículo y misma carrocería que la línea): la línea queda pendiente.";
            justificacion += " " + texto;
            motivo ??= texto;
        }

        return dto with
        {
            RetornoContenedor = retorno, AprovisionamientoContenedor = aprovisionamiento,
            Justificacion = justificacion, MotivoPendiente = motivo,
        };
    }

    /// <summary>Motivo visible cuando el piso SICE-TAC no se usa por período (ver <see cref="IPropuestaTarifaPort.MotivoSicetacNoUsadoAsync"/>). Null si DWSice no está configurado.</summary>
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
        var (fila, criterio, vehiculoRelajado) = await ResolverFilaSiceAsync(dw, origen, destino, origenDane, destinoDane, vehiculo, condicionCarga, tipologia, carroceria, ct);
        if (fila is null) return null;

        // Piso recalculado con las horas reales de la solicitud en vez de las pactadas (criterio de fallback en clsPisoConHoras).
        var valor = clsPisoConHoras.Calcular(fila.Valor, fila.ValorHora, fila.HorasPactadas, fila.ValorSiceConHora, horasSolicitud);
        return new clsCandidatoTarifaDto(
            "sicetac", valor,
            $"{fila.NombreRuta} · {fila.Configuracion} · {fila.NombreTipoCarga} · {fila.NombreUnidadTransporte}",
            criterio, 1,
            Advertencia: vehiculoRelajado ? AdvertenciaVehiculoRelajado(vehiculo) : null,
            ValorPisoBase: fila.Valor, ValorTiemposLogisticos: valor - fila.Valor, Configuracion: fila.Configuracion,
            VehiculoRelajado: vehiculoRelajado);
    }

    private const string CarroceriaDeContenedor = "PORTACONTENEDORES";

    /// <summary>Una línea de contenedor viaja en portacontenedores: es la carrocería por defecto cuando la línea no trae otra.</summary>
    private static string? CarroceriaPorDefectoDeContenedor(bool esContenedor) => esContenedor ? CarroceriaDeContenedor : null;

    /// <summary>
    /// Vehículo que corresponde a una línea de contenedor sin vehículo, según tamaño y peso (<c>tbl_rangos_peso_contenedor</c>); sin él la búsqueda tolerante tomaría la fila más barata de cualquier vehículo.
    /// Null si la línea ya trae vehículo, no es de contenedor, faltan tamaño o peso, la tabla no está disponible o ningún vehículo aplica.
    /// </summary>
    private async Task<string?> VehiculoDeContenedorSinVehiculoAsync(clsLineaSolicitud linea, string? vehiculo, bool esContenedor, CancellationToken ct)
    {
        if (!esContenedor || !string.IsNullOrWhiteSpace(vehiculo) || linea.Peso is not { } peso) return null;
        if (clsRecomendadorDeVehiculoDeContenedor.Pies(linea.ClaseContenedor) is not { } pies) return null;
        return (await _rangos.VehiculoParaAsync(pies, peso, ct)).TipoVehiculo;
    }

    private static string AdvertenciaVehiculoRelajado(string? vehiculo) =>
        $"No hay filas del vehículo {vehiculo?.Trim().ToUpper()} para esta ruta: el valor corresponde a otro vehículo y solo sirve de referencia.";

    /// <summary>
    /// Piso SICE-TAC de un viaje VACÍO: la fila se elige por la carrocería de la línea (nunca la más barata); sin carrocería solo se acepta en contenedor (PORTACONTENEDORES).
    /// Con <paramref name="permitirVehiculoRelajado"/> en false el vehículo debe coincidir exacto (retorno y levante); en true se usa otro vehículo y el candidato sale marcado como relajado. Null si nada coincide.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> CandidatoVacioAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane,
        string? vehiculo, string? carroceria, bool esContenedor, bool permitirVehiculoRelajado, decimal horas, CancellationToken ct)
    {
        var pidioVehiculo = !string.IsNullOrWhiteSpace(vehiculo);
        if (!pidioVehiculo && !permitirVehiculoRelajado) return null;

        var consulta = await PrepararConsultaSiceAsync(dw, origen, destino, origenDane, destinoDane, "VACIO", ct);
        if (consulta is null) return null;
        var (baseQ, criterio, claveBase) = consulta.Value;

        clsRutaSicetacDw? fila = null;
        var relajado = false;
        if (pidioVehiculo)
        {
            // El código con el que SICE-TAC conoce al vehículo (el Sencillo C2 es 2 en SICE-TAC); sin mapeo, el del propio vehículo.
            var config = (await _mapeo.SicetacAsync(vehiculo, ct: ct))!;
            fila = SeleccionarSiceVacio(await FilasSiceCacheadasAsync($"{claveBase}:{config}", baseQ.Where(r => r.Configuracion == config), ct), carroceria, esContenedor);
            if (fila is not null) criterio += $" · configuración {config}";
        }
        if (fila is null && permitirVehiculoRelajado)
        {
            fila = SeleccionarSiceVacio(await FilasSiceCacheadasAsync($"{claveBase}:*", baseQ, ct), carroceria, esContenedor);
            relajado = pidioVehiculo && fila is not null;
        }
        if (fila is null) return null;

        var valor = clsPisoConHoras.Calcular(fila.Valor, fila.ValorHora, fila.HorasPactadas, fila.ValorSiceConHora, horas);
        return new clsCandidatoTarifaDto(
            "sicetac", valor,
            $"{fila.NombreRuta} · {fila.Configuracion} · {fila.NombreTipoCarga} · {fila.NombreUnidadTransporte}",
            criterio + $" · unidad {fila.NombreUnidadTransporte}", 1,
            Advertencia: relajado ? AdvertenciaVehiculoRelajado(vehiculo) : null,
            ValorPisoBase: fila.Valor, ValorTiemposLogisticos: valor - fila.Valor, Configuracion: fila.Configuracion,
            VehiculoRelajado: relajado);
    }

    /// <summary>
    /// Elige la fila de la unidad de transporte que corresponde a la carrocería (sin tildes ni mayúsculas, "_" y "-" como espacio); entre ellas prefiere vía estándar, menores horas y menor valor.
    /// Sin carrocería solo elige en una línea de contenedor (PORTACONTENEDORES); null si la unidad no existe.
    /// </summary>
    internal static clsRutaSicetacDw? SeleccionarSiceVacio(List<clsRutaSicetacDw> filas, string? carroceria, bool esContenedor)
    {
        const string UnidadContenedor = "PORTACONTENEDORES";
        var unidad = string.IsNullOrWhiteSpace(carroceria)
            ? (esContenedor ? UnidadContenedor : null)
            : clsResolucionDeCatalogos.Normalizar(carroceria);
        if (unidad is null) return null;

        return filas
            .Where(r => (r.ValorSiceConHora ?? r.Valor) > 0 && clsResolucionDeCatalogos.Normalizar(r.NombreUnidadTransporte) == unidad)
            .OrderBy(r => string.Equals(r.ViaEstandar, "SI", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.HorasRecorrido ?? decimal.MaxValue)
            .ThenBy(r => r.Valor)
            .FirstOrDefault();
    }

    /// <summary>Piso SICE-TAC vigente con desglose de horas para Mesa de Costeo; usa la misma fuente DWSice que el motor automático.</summary>
    public async Task<clsPisoSicetacVigenteDto?> PisoSicetacVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct = default)
    {
        if (!_dwConfigurado) return null;
        using var scope = _scopes.CreateScope();
        var dw = scope.ServiceProvider.GetService<clsDwSiceDbContext>();
        if (dw is null) return null;

        var (fila, _, _) = await ResolverFilaSiceAsync(
            dw, Normalizar(origen), Normalizar(destino), origenDane: null, destinoDane: null, vehiculo, "CARGADO", tipologia, carroceria, ct);
        if (fila is null) return null;

        return new clsPisoSicetacVigenteDto(
            fila.Periodo, fila.Valor, fila.ValorHora, fila.HorasPactadas,
            fila.Configuracion, fila.NombreTipoCarga, fila.NombreUnidadTransporte,
            fila.CondicionCarga ?? "CARGADO");
    }

    /// <summary>
    /// Resuelve la fila de DWSice, compartida por la propuesta automática y Mesa de Costeo (<see cref="PisoSicetacVigenteAsync"/>). Con <paramref name="origenDane"/>/<paramref name="destinoDane"/> compara EXACTO contra tblRutasSicetac.ORIGEN/DESTINO
    /// (que guarda el DANE sin el cero a la izquierda del departamento, de ahí el TrimStart('0')); sin DANE cae al LIKE sobre NOMBRERUTA. El filtro exacto es además mucho más barato en ~2.1 M de filas por periodo.
    /// </summary>
    private async Task<(clsRutaSicetacDw? Fila, string Criterio, bool VehiculoRelajado)> ResolverFilaSiceAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane,
        string? vehiculo, string condicionCarga, TipologiaCarga? tipologia, string? carroceria, CancellationToken ct)
    {
        var consulta = await PrepararConsultaSiceAsync(dw, origen, destino, origenDane, destinoDane, condicionCarga, ct);
        if (consulta is null) return (null, string.Empty, false);
        var (baseQ, criterio, claveBase) = consulta.Value;

        clsRutaSicetacDw? fila = null;
        var pidioVehiculo = !string.IsNullOrWhiteSpace(vehiculo);
        if (pidioVehiculo)
        {
            // El código con el que SICE-TAC conoce al vehículo (el Sencillo C2 es 2 en SICE-TAC); sin mapeo, el del propio vehículo.
            var config = (await _mapeo.SicetacAsync(vehiculo, ct: ct))!;
            fila = SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:{config}", baseQ.Where(r => r.Configuracion == config), ct), tipologia, carroceria);
            if (fila is not null) criterio += $" · configuración {config}";
        }

        // Si el vehículo pedido no tiene filas en la ruta se usa cualquier otro, pero el candidato queda marcado como relajado.
        var relajado = false;
        if (fila is null)
        {
            fila = SeleccionarSice(await FilasSiceCacheadasAsync($"{claveBase}:*", baseQ, ct), tipologia, carroceria);
            relajado = pidioVehiculo && fila is not null;
        }
        return (fila, criterio, relajado);
    }

    /// <summary>
    /// Consulta de filas SICE-TAC de una ruta y condición de carga (CARGADO o VACIO) del período del mes actual: por código DANE exacto si hay municipios resueltos, si no por texto de la ruta.
    /// Null si el período más reciente cargado no es el del mes actual.
    /// </summary>
    private async Task<(IQueryable<clsRutaSicetacDw> Consulta, string Criterio, string ClaveBase)?> PrepararConsultaSiceAsync(
        clsDwSiceDbContext dw, string origen, string destino, string? origenDane, string? destinoDane,
        string condicionCarga, CancellationToken ct)
    {
        // Solo se usa el período del mes y año actuales (sin mes de gracia): si el más reciente cargado no lo es, no hay piso.
        var periodo = await PeriodoSiceAsync(dw, ct);
        if (!clsPeriodoSicetac.EsElDelMesActual(periodo, _clock.GetUtcNow())) return null;

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
        return (baseQ, criterio, $"sice-filas:{periodo}:{condicionCarga}:{claveRuta}");
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
            // La carrocería se compara sin tildes ni mayúsculas y con "_" y "-" como espacio ("Furgón" = "FURGON"); una comparación literal no hallaría la unidad.
            var unidad = clsResolucionDeCatalogos.Normalizar(carroceria);
            var porUnidad = cand.Where(r => clsResolucionDeCatalogos.Normalizar(r.NombreUnidadTransporte) == unidad).ToList();
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

    /// <summary>Período AAAAMM más reciente cargado en DWSice hasta el mes actual (0 si no hay); un período futuro no tapa al vigente. La clave de caché lleva el período actual para no reusar el del mes anterior.</summary>
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

    /// <summary>Resuelve el NOMBRE (no el Id ni el enum) de línea de negocio que guardan tbl_tarifas_plaza/tbl_tarifas_plaza_historico, con el mismo mapeo enum-&gt;Id SAP de clsTarifarioSapAdapter sobre ILineasNegocioPort.</summary>
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

    /// <summary>Códigos de zona SAP (ZC_/ZTC_) de un municipio ya resuelto (clsMunicipio.NombreTarifasPlaza/ZonaTarificacion). Vacío si el municipio es null o no tiene ninguno.</summary>
    private static IReadOnlyList<string> CodigosZonaSap(clsMunicipio? municipio) =>
        new[] { municipio?.NombreTarifasPlaza, municipio?.ZonaTarificacion }
            .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();

    /// <summary>
    /// Origen/Destino de tblSAPTarifas/tblSAPTarifasLog guardan código DIVIPOLA o zona SAP (<see cref="Nexo.Infrastructure.External.clsSapTarifaBase"/>): se compara EXACTO contra los códigos ZC_/ZTC_ de tbl_municipios.
    /// Sin municipio resuelto en alguno de los lados, o sin código de zona, devuelve null (la fuente no aplica); nunca se adivina.
    /// </summary>
    private async Task<clsCandidatoTarifaDto?> CandidatoSapAsync<T>(
        clsDbSolucionesTiDbContext dc, IQueryable<T> tabla, string fuente,
        clsMunicipio? origenMunicipio, clsMunicipio? destinoMunicipio,
        string? vehiculo, decimal? peso, bool soloVigentes,
        IReadOnlyDictionary<int, DateTime> ajustesVigencia, CancellationToken ct,
        int? ventanaMeses = null, string? criterioExtra = null, bool exigeVehiculoDePlaza = false) where T : clsSapTarifaBase
    {
        var codigosOrigen = CodigosZonaSap(origenMunicipio);
        var codigosDestino = CodigosZonaSap(destinoMunicipio);
        if (codigosOrigen.Count == 0 || codigosDestino.Count == 0) return null;

        var query = tabla.Where(t => codigosOrigen.Contains(t.Origen) && codigosDestino.Contains(t.Destino));

        DateTime Efectiva(T t) => ajustesVigencia.TryGetValue(t.Id, out var v) ? v : t.Vigencia;

        var hoy = _clock.GetUtcNow().Date;
        var criterio = $"origen zona ({string.Join(",", codigosOrigen)}) · destino zona ({string.Join(",", codigosDestino)})";
        if (criterioExtra is not null) criterio += $" · {criterioExtra}";
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

        var vehiculoPedido = !string.IsNullOrWhiteSpace(vehiculo);
        var vehiculoAplicado = false;
        if (vehiculoPedido)
        {
            // El código con el que Plaza/SAP conoce al vehículo (el Sencillo C2 es 2; el Turbo 2L1/2L3 se tarifa como 2L2). Un vehículo mapeado
            // sin código de plaza no tiene tarifa ahí: en una línea de cupo, que se define por el vehículo, no se ofrece la de otro.
            var plaza = await _mapeo.PlazaAsync(vehiculo, ct);
            if (plaza.SinTarifaDePlaza && exigeVehiculoDePlaza) return null;
            var idVehiculo = plaza.SinTarifaDePlaza
                ? null
                : await IdVehiculoSapAsync(dc, plaza.Codigo!, ct) ?? await IdVehiculoSapAsync(dc, vehiculo!, ct);
            if (idVehiculo is not null)
            {
                var conVehiculo = query.Where(t => t.TipoVehiculo == idVehiculo);
                if (await conVehiculo.AnyAsync(ct))
                {
                    query = conVehiculo;
                    criterio += $" · vehículo {vehiculo!.Trim().ToUpper()}";
                    vehiculoAplicado = true;
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
        // El vehículo se pidió pero la fuente no tiene tarifas de ese vehículo en la ruta: el valor es de otro vehículo.
        var relajado = vehiculoPedido && !vehiculoAplicado;
        return new clsCandidatoTarifaDto(
            fuente, top.Valor!.Value,
            $"{top.Fila.Origen} → {top.Fila.Destino} · vigencia {vigenciaEfectiva:yyyy-MM-dd}{ajustada}" +
                (top.Fila is clsSapTarifaPlaza { DetallesTarifa: { Length: > 0 } det } ? $" · {det}" : ""),
            criterio, candidatas.Count,
            Advertencia: relajado ? AdvertenciaVehiculoRelajado(vehiculo) : null,
            VehiculoRelajado: relajado);
    }

    /// <summary>
    /// Código DANE del municipio colombiano de una ciudad escrita a mano (levante o devolución): primero nombre exacto; si no, municipios cuyo nombre contiene el texto o está contenido en él ("Bogotá" ~ "BOGOTA, D.C.").
    /// Null si no hay ninguno o hay más de uno distinto: una ciudad ambigua no se adivina y el tramo se busca por texto.
    /// </summary>
    private async Task<string?> ResolverDaneCiudadAsync(clsNexoDbContext db, string ciudad, CancellationToken ct)
    {
        var municipios = await _cache.GetOrCreateAsync("prop:municipios-colombia", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await db.Municipios.AsNoTracking().Where(m => m.Pais == "CO")
                .Select(m => new { m.CodigoDane, m.Municipio }).ToListAsync(ct);
            return lista.Select(m => (m.CodigoDane, Nombre: clsResolucionDeCatalogos.Normalizar(m.Municipio))).ToList();
        });

        var texto = clsResolucionDeCatalogos.Normalizar(ciudad);
        if (texto.Length == 0) return null;

        var exactos = municipios!.Where(m => m.Nombre == texto).Select(m => m.CodigoDane).Distinct().ToList();
        if (exactos.Count == 1) return exactos[0];
        if (exactos.Count > 1) return null;

        var contenidos = municipios!.Where(m => m.Nombre.Length > 0 && (m.Nombre.Contains(texto) || texto.Contains(m.Nombre)))
            .Select(m => m.CodigoDane).Distinct().ToList();
        return contenidos.Count == 1 ? contenidos[0] : null;
    }

    /// <summary>Id SAP de una clase de contenedor a partir de su descripción (por ejemplo CNT20.IT). Null si no se reconoce.</summary>
    private async Task<int?> IdClaseContenedorSapAsync(clsDbSolucionesTiDbContext dc, string? claseContenedor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(claseContenedor)) return null;
        var mapa = await _cache.GetOrCreateAsync("prop:sap-clases-contenedor", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = CacheTtl;
            var lista = await dc.ClasesContenedor.Select(x => new { x.Id, x.Descripcion }).ToListAsync(ct);
            return lista.GroupBy(x => Normalizar(x.Descripcion)).ToDictionary(g => g.Key, g => g.First().Id);
        });
        return mapa!.TryGetValue(Normalizar(claseContenedor), out var id) ? id : null;
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
