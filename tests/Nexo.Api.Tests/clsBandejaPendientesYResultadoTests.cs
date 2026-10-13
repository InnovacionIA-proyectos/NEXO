using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-23.1/#289 contra la BD real: (1) "Pendientes" suma la solicitud con todas las líneas
/// revisadas y sin entregar; (2) vigencia por Fecha Límite; (3) columna y filtro Resultado.
///
/// Cada prueba crea un Comercial propio y siembra SOLO solicitudes suyas: la Bandeja de un Comercial
/// ya se limita a lo que él creó (issue #199), así que los resultados son exactos y no dependen de lo
/// que haya en la base compartida. Todo se borra al terminar.
/// </summary>
[Collection("api")]
public class clsBandejaPendientesYResultadoTests
{
    private readonly clsApiFixture _fx;
    public clsBandejaPendientesYResultadoTests(clsApiFixture fx) => _fx = fx;

    private sealed class clsEscenario : IDisposable
    {
        private readonly clsApiFixture _fx;
        public Guid Comercial { get; } = Guid.NewGuid();
        public Guid OtroComercial { get; } = Guid.NewGuid();
        public Dictionary<string, Guid> Solicitudes { get; } = new();

        public clsEscenario(clsApiFixture fx)
        {
            _fx = fx;
            _fx.WithDb(db =>
            {
                foreach (var id in new[] { Comercial, OtroComercial })
                    db.Usuarios.Add(new clsUsuario
                    {
                        Id = id, Nombre = "Comercial 289 " + id.ToString("N")[..6],
                        Email = $"c289-{id:N}@boterosoto.com.co", Rol = RolUsuario.Comercial,
                    });
                return db.SaveChanges();
            });
        }

        /// <summary>Crea una solicitud del Comercial con una línea (estado dado), con o sin flete en la cotización.</summary>
        public Guid Crear(string clave, EstadoSolicitud estado, EstadoLinea linea, bool conFlete,
            DateTimeOffset? deadline = null, int diasDeCreada = 0, Guid? creador = null,
            Action<clsSolicitud, clsCotizacion?>? ajustar = null, EstadoCotizacion? cotizacion = EstadoCotizacion.Borrador,
            ResultadoCotizacion resultado = ResultadoCotizacion.SinDefinir, bool autoCosteo = false)
        {
            var id = _fx.WithDb(db =>
            {
                var s = new clsSolicitud
                {
                    ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = estado,
                    CreadoPor = creador ?? Comercial, Deadline = deadline,
                };
                var l = new clsLineaSolicitud { SolicitudId = s.Id, EstadoLinea = linea, Peso = 1000 };
                s.Lineas.Add(l);
                clsCotizacion? c = null;
                if (cotizacion is { } est)
                {
                    c = new clsCotizacion { SolicitudId = s.Id, Estado = est, Resultado = resultado, EsAutoCosteo = autoCosteo };
                    if (conFlete) c.Lineas.Add(new clsCotizacionLinea { LineaSolicitudId = l.Id, ValorFlete = 1_000_000m, Fuente = FuenteFlete.Manual });
                    s.Cotizaciones.Add(c);
                }
                ajustar?.Invoke(s, c);
                db.Solicitudes.Add(s);
                db.SaveChanges();
                // El SaveChanges de NEXO estampa CreatedAt = ahora: la antigüedad se fija después.
                if (diasDeCreada > 0)
                {
                    var creada = DateTimeOffset.UtcNow.AddDays(-diasDeCreada);
                    db.Solicitudes.Where(x => x.Id == s.Id).ExecuteUpdate(x => x.SetProperty(y => y.CreatedAt, creada));
                }
                return s.Id;
            });
            Solicitudes[clave] = id;
            return id;
        }

        public void Dispose() => _fx.WithDb(db =>
        {
            var ids = Solicitudes.Values.ToList();
            db.Cotizaciones.Where(c => ids.Contains(c.SolicitudId)).ExecuteDelete();
            db.LineasSolicitud.Where(l => ids.Contains(l.SolicitudId)).ExecuteDelete();
            db.Solicitudes.Where(s => ids.Contains(s.Id)).ExecuteDelete();
            var usuarios = new[] { Comercial, OtroComercial };
            db.Usuarios.Where(u => usuarios.Contains(u.Id)).ExecuteDelete();
            return 0;
        });
    }

    private async Task<JsonElement> Bandeja(Guid comercial, string consulta, string rol = "Comercial")
    {
        var resp = await _fx.Client(rol, comercial).GetAsync($"/api/solicitudes?pageSize=200&{consulta}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Json();
    }

    private static HashSet<Guid> Ids(JsonElement pagina) =>
        pagina.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToHashSet();

    // ------------------------------------------------------------------ Pendientes

    [Fact]
    public async Task Pendientes_suma_la_solicitud_con_todas_las_lineas_revisadas_y_sin_entregar_y_sigue_en_atendidas_y_administrador()
    {
        using var e = new clsEscenario(_fx);
        var sinFlete = e.Crear("sinFlete", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, conFlete: false);
        var revisadaSinEntregar = e.Crear("revisada", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, conFlete: true);
        var revisadaEntregada = e.Crear("entregada", EstadoSolicitud.Entregada, EstadoLinea.Revisada, conFlete: true, cotizacion: EstadoCotizacion.Entregada);
        var auto = e.Crear("auto", EstadoSolicitud.EnCosteo, EstadoLinea.AutoResuelta, conFlete: true, autoCosteo: true);
        var revisadaSinCotLinea = e.Crear("revisadaSinFlete", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, conFlete: false);

        var pendientes = Ids(await Bandeja(e.Comercial, "atencion=pendientes"));
        var porDefecto = Ids(await Bandeja(e.Comercial, ""));
        var atendidas = Ids(await Bandeja(e.Comercial, "atencion=atendidas"));
        var administrador = Ids(await Bandeja(e.Comercial, "atencion=administrador"));
        var automatico = Ids(await Bandeja(e.Comercial, "atencion=automatico"));

        // Pendientes: sin flete (como siempre) + la revisada completa sin entregar. NO la entregada, NO la automática.
        Assert.Equal(new HashSet<Guid> { sinFlete, revisadaSinEntregar, revisadaSinCotLinea }, pendientes);
        Assert.Equal(pendientes, porDefecto);   // sin parámetros = la cola de trabajo

        // Sigue apareciendo donde aparecía (no es una categoría nueva).
        Assert.Contains(revisadaSinEntregar, atendidas);
        Assert.Contains(revisadaSinEntregar, administrador);
        Assert.Contains(revisadaEntregada, administrador);

        // La automática no cambia de comportamiento: ni en Pendientes, y sigue en Automático.
        Assert.DoesNotContain(auto, pendientes);
        Assert.Contains(auto, automatico);
        Assert.DoesNotContain(revisadaSinEntregar, automatico);
    }

    [Fact]
    public async Task Al_pasar_a_Entregada_la_solicitud_revisada_deja_de_salir_en_Pendientes()
    {
        using var e = new clsEscenario(_fx);
        var id = e.Crear("s", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, conFlete: true);
        Assert.Contains(id, Ids(await Bandeja(e.Comercial, "atencion=pendientes")));

        _fx.WithDb(db => db.Solicitudes.Where(s => s.Id == id).ExecuteUpdate(s => s.SetProperty(x => x.Estado, EstadoSolicitud.Entregada)));

        Assert.DoesNotContain(id, Ids(await Bandeja(e.Comercial, "atencion=pendientes")));
        Assert.Contains(id, Ids(await Bandeja(e.Comercial, "atencion=administrador")));
    }

    [Fact]
    public async Task Pendientes_con_el_OR_no_mezcla_solicitudes_de_otro_comercial_y_los_demas_roles_ven_todo()
    {
        using var e = new clsEscenario(_fx);
        var mia = e.Crear("mia", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, conFlete: true);
        var ajena = e.Crear("ajena", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, conFlete: true, creador: e.OtroComercial);
        var ajenaSinFlete = e.Crear("ajenaSinFlete", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, conFlete: false, creador: e.OtroComercial);

        var delComercial = Ids(await Bandeja(e.Comercial, "atencion=pendientes"));
        var delOtro = Ids(await Bandeja(e.OtroComercial, "atencion=pendientes"));
        // Admin ve toda la base compartida: se acota a lo creado durante esta prueba para que entre en la página.
        var admin = Ids(await Bandeja(Guid.NewGuid(), $"atencion=pendientes&desde={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"))}", rol: "Admin"));

        Assert.Equal(new HashSet<Guid> { mia }, delComercial);
        Assert.Equal(new HashSet<Guid> { ajena, ajenaSinFlete }, delOtro);
        Assert.Superset(new HashSet<Guid> { mia, ajena, ajenaSinFlete }, admin);   // el resto de roles ve todo
    }

    // ------------------------------------------------------------------ vigencia

    [Fact]
    public async Task La_vigencia_es_la_de_la_Fecha_Limite_y_sin_Deadline_es_vigente()
    {
        using var e = new clsEscenario(_fx);
        var ahora = DateTimeOffset.UtcNow;
        // Captura vieja pero Deadline futuro; captura de hoy pero Deadline vencido; sin Deadline y vieja.
        var futuraVieja = e.Crear("futuraVieja", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, false, deadline: ahora.AddDays(10), diasDeCreada: 30);
        var vencidaNueva = e.Crear("vencidaNueva", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, false, deadline: ahora.AddDays(-2));
        var sinDeadline = e.Crear("sinDeadline", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, false, deadline: null, diasDeCreada: 30);

        var vigentes = Ids(await Bandeja(e.Comercial, "atencion=todas&vigencia=vigente"));
        var vencidas = Ids(await Bandeja(e.Comercial, "atencion=todas&vigencia=vencida"));
        var todas = await Bandeja(e.Comercial, "atencion=todas");

        Assert.Equal(new HashSet<Guid> { futuraVieja, sinDeadline }, vigentes);
        Assert.Equal(new HashSet<Guid> { vencidaNueva }, vencidas);
        var porId = todas.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetGuid(), i => i.GetProperty("vigencia").GetString());
        Assert.Equal("vigente", porId[futuraVieja]);
        Assert.Equal("vencida", porId[vencidaNueva]);
        Assert.Equal("vigente", porId[sinDeadline]);
    }

    // ------------------------------------------------------------------ Resultado

    private static (Guid esperandoEntregada, Guid esperandoAuto, Guid ganada, Guid perdida, Guid descartada, Guid borrador, Guid sinCot, Guid mixta)
        Resultados(clsEscenario e)
    {
        var esperandoEntregada = e.Crear("r1", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada);
        var esperandoAuto = e.Crear("r5", EstadoSolicitud.EnCosteo, EstadoLinea.AutoResuelta, true, autoCosteo: true);
        var ganada = e.Crear("r2", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada, resultado: ResultadoCotizacion.Ganada);
        var perdida = e.Crear("r3", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada, resultado: ResultadoCotizacion.Perdida);
        var descartada = e.Crear("r4", EstadoSolicitud.Descartada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada);
        var borrador = e.Crear("r6", EstadoSolicitud.EnCosteo, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Borrador);
        var sinCot = e.Crear("r7", EstadoSolicitud.Pendiente, EstadoLinea.PorCostear, false, cotizacion: null);
        // Dos cotizaciones: una Ganada (vieja, entregada) y un borrador SinDefinir más reciente.
        var mixta = e.Crear("r8", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada, resultado: ResultadoCotizacion.Ganada,
            ajustar: (s, _) => s.Cotizaciones.Add(new clsCotizacion { SolicitudId = s.Id, Estado = EstadoCotizacion.Borrador }));
        return (esperandoEntregada, esperandoAuto, ganada, perdida, descartada, borrador, sinCot, mixta);
    }

    [Fact]
    public async Task El_filtro_esperando_lista_solo_entregadas_o_autocosteadas_sin_resultado_y_no_descartadas()
    {
        using var e = new clsEscenario(_fx);
        var r = Resultados(e);

        var esperando = Ids(await Bandeja(e.Comercial, "resultado=esperando"));   // sin atencion: se trata como "todas"

        // La entregada (aunque la solicitud esté Entregada y por eso no salga en la vista por defecto) y la auto-costeada.
        Assert.Equal(new HashSet<Guid> { r.esperandoEntregada, r.esperandoAuto }, esperando);
        // Excluye: Ganada, Perdida, descartada, borrador manual, sin cotización y la mixta (que ya tiene una Ganada).
        Assert.DoesNotContain(r.ganada, esperando);
        Assert.DoesNotContain(r.perdida, esperando);
        Assert.DoesNotContain(r.descartada, esperando);
        Assert.DoesNotContain(r.borrador, esperando);
        Assert.DoesNotContain(r.sinCot, esperando);
        Assert.DoesNotContain(r.mixta, esperando);
    }

    [Fact]
    public async Task Los_filtros_ganada_y_perdida_listan_solo_esas()
    {
        using var e = new clsEscenario(_fx);
        var r = Resultados(e);

        var ganadas = Ids(await Bandeja(e.Comercial, "resultado=ganada"));
        var perdidas = Ids(await Bandeja(e.Comercial, "resultado=perdida"));

        Assert.Equal(new HashSet<Guid> { r.ganada, r.mixta }, ganadas);
        Assert.Equal(new HashSet<Guid> { r.perdida }, perdidas);
    }

    [Fact]
    public async Task Cada_fila_trae_resultado_y_con_varias_cotizaciones_gana_la_que_tiene_resultado()
    {
        using var e = new clsEscenario(_fx);
        var r = Resultados(e);

        var todas = await Bandeja(e.Comercial, "atencion=todas");

        var porId = todas.GetProperty("items").EnumerateArray().ToDictionary(
            i => i.GetProperty("id").GetGuid(), i => i.GetProperty("resultado").ValueKind == JsonValueKind.Null ? null : i.GetProperty("resultado").GetString());
        Assert.Equal("SinDefinir", porId[r.esperandoEntregada]);
        Assert.Equal("SinDefinir", porId[r.esperandoAuto]);
        Assert.Equal("Ganada", porId[r.ganada]);
        Assert.Equal("Perdida", porId[r.perdida]);
        Assert.Null(porId[r.sinCot]);
        Assert.Equal("Ganada", porId[r.mixta]);   // la cotización con resultado, no el borrador más reciente
    }

    [Fact]
    public async Task Los_filtros_de_resultado_con_rol_Comercial_solo_ven_lo_suyo_y_los_demas_roles_ven_todo()
    {
        using var e = new clsEscenario(_fx);
        var mia = e.Crear("mia", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada);
        var ajena = e.Crear("ajena", EstadoSolicitud.Entregada, EstadoLinea.Revisada, true, cotizacion: EstadoCotizacion.Entregada, creador: e.OtroComercial);

        var delComercial = Ids(await Bandeja(e.Comercial, "resultado=esperando"));
        var delOtro = Ids(await Bandeja(e.OtroComercial, "resultado=esperando"));
        var admin = Ids(await Bandeja(Guid.NewGuid(), $"resultado=esperando&desde={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"))}", rol: "AdminTarifas"));

        Assert.Equal(new HashSet<Guid> { mia }, delComercial);
        Assert.Equal(new HashSet<Guid> { ajena }, delOtro);
        Assert.Superset(new HashSet<Guid> { mia, ajena }, admin);
    }

    [Fact]
    public async Task El_resultado_se_combina_con_los_otros_filtros_y_un_valor_invalido_da_400()
    {
        using var e = new clsEscenario(_fx);
        var r = Resultados(e);

        // Combinado con una atención explícita: "esperando" + "automatico" deja solo la auto-costeada.
        var combinado = Ids(await Bandeja(e.Comercial, "resultado=esperando&atencion=automatico"));
        Assert.Equal(new HashSet<Guid> { r.esperandoAuto }, combinado);

        var malo = await _fx.Client("Comercial", e.Comercial).GetAsync("/api/solicitudes?resultado=tal_vez");
        Assert.Equal(HttpStatusCode.BadRequest, malo.StatusCode);
        Assert.Equal("RESULTADO_INVALIDO", await malo.ErrorCode());
    }
}
