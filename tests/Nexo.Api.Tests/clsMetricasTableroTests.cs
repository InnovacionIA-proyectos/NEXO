using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Common.Mensajeria.Saga;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-21.1/#287 de extremo a extremo contra la BD real de pruebas: el filtro de fecha del
/// Tablero (tarjetas de la cola por creación, Cumplimiento SLA por entrega) y el tiempo de
/// costeo híbrido por tamaño de pliego. Cada prueba siembra sus filas en una ventana de fechas
/// propia (años 2031-2032, sin datos reales) para aislarse de lo que ya haya en la base, y las
/// borra al terminar.
/// </summary>
[Collection("api")]
public class clsMetricasTableroTests
{
    private readonly clsApiFixture _fx;
    public clsMetricasTableroTests(clsApiFixture fx) => _fx = fx;

    private static DateTimeOffset T(int anio, int mes, int dia, int hora = 0, int min = 0) =>
        new(anio, mes, dia, hora, min, 0, TimeSpan.Zero);

    private static string Q(DateTimeOffset d) => Uri.EscapeDataString(d.ToString("o"));

    /// <summary>Siembra, ejecuta y limpia. Todo lo sembrado se borra aunque la aserción falle.</summary>
    private async Task ConDatos(Func<clsSiembra, (List<clsSolicitud>, List<clsCotizacion>, List<clsEstadoAutoCosteo>)> sembrar, Func<Task> prueba)
    {
        List<clsSolicitud> sols = []; List<clsCotizacion> cots = []; List<clsEstadoAutoCosteo> sagas = [];
        _fx.WithDb(db =>
        {
            (sols, cots, sagas) = sembrar(new clsSiembra(db.Clientes.First().Id, db.Usuarios.First().Id));
            // El SaveChanges de NEXO estampa CreatedAt = ahora al insertar (auditoría), así que la
            // fecha de creación que la prueba necesita se fija DESPUÉS con ExecuteUpdate, que no
            // pasa por ese estampado.
            var creadas = sols.ToDictionary(s => s.Id, s => s.CreatedAt);
            db.Solicitudes.AddRange(sols); db.Cotizaciones.AddRange(cots); db.AddRange(sagas);
            db.SaveChanges();
            foreach (var (id, creada) in creadas)
                db.Solicitudes.Where(s => s.Id == id).ExecuteUpdate(s => s.SetProperty(x => x.CreatedAt, creada));
            return 0;
        });
        try { await prueba(); }
        finally
        {
            _fx.WithDb(db =>
            {
                db.RemoveRange(db.Set<clsEstadoAutoCosteo>().Where(s => sagas.Select(x => x.CorrelationId).Contains(s.CorrelationId)));
                db.RemoveRange(db.Cotizaciones.Where(c => cots.Select(x => x.Id).Contains(c.Id)));
                db.RemoveRange(db.Solicitudes.Where(s => sols.Select(x => x.Id).Contains(s.Id)));
                return db.SaveChanges();
            });
        }
    }

    private sealed record clsSiembra(Guid ClienteId, Guid UsuarioId)
    {
        public clsSolicitud Solicitud(DateTimeOffset creada, EstadoSolicitud estado, DateTimeOffset? deadline = null) => new()
        {
            ClienteId = ClienteId, CreadoPor = UsuarioId, Tipo = TipoSolicitud.Spot, Estado = estado,
            CreatedAt = creada, Deadline = deadline,
        };

        public clsCotizacion Entregada(clsSolicitud s, DateTimeOffset en) => new()
        {
            SolicitudId = s.Id, Estado = EstadoCotizacion.Entregada, EntregadaAt = en,
        };

        public clsEstadoAutoCosteo Saga(string estado, Guid solicitudId, DateTimeOffset solicitado, DateTimeOffset terminado, int lineas) => new()
        {
            CorrelationId = Guid.NewGuid(), CurrentState = estado, SolicitudId = solicitudId, SolicitadoPor = UsuarioId,
            SolicitadoEn = solicitado, TerminadoEn = terminado, TotalLineas = lineas,
        };
    }

    // ------------------------------------------------------------ Cumplimiento: filtro de fecha

    [Fact]
    public async Task Sin_filtro_el_cumplimiento_se_comporta_como_antes_y_desde_hasta_vienen_null()
    {
        var json = await (await _fx.Client("LiderProceso").GetAsync("/api/metricas/cumplimiento")).Json();

        Assert.Equal(JsonValueKind.Null, json.GetProperty("desde").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("hasta").ValueKind);
        Assert.True(json.GetProperty("totalSolicitudes").GetInt32() > 0);
        Assert.True(json.TryGetProperty("cumplimientoSla", out _));
    }

    [Fact]
    public async Task Con_desde_hasta_las_tarjetas_cuentan_solicitudes_creadas_en_el_rango_y_el_SLA_cotizaciones_entregadas_en_el_rango()
    {
        var desde = T(2031, 3, 1); var hasta = T(2031, 3, 31, 23, 59);
        await ConDatos(p =>
        {
            // Creadas DENTRO del rango: una por estado de la cola.
            var pend = p.Solicitud(T(2031, 3, 10), EstadoSolicitud.Pendiente);
            var cost = p.Solicitud(T(2031, 3, 11), EstadoSolicitud.EnCosteo);
            var desc = p.Solicitud(T(2031, 3, 12), EstadoSolicitud.Descartada);
            var entA = p.Solicitud(T(2031, 3, 13), EstadoSolicitud.Entregada, deadline: T(2031, 3, 20));
            // Creada FUERA del rango (abril): no cuenta en ninguna tarjeta de la cola.
            var fuera = p.Solicitud(T(2031, 4, 10), EstadoSolicitud.Pendiente);
            // Creada ANTES del rango pero ENTREGADA dentro: cuenta en SLA (por EntregadaAt), no en la tarjeta.
            var entB = p.Solicitud(T(2031, 2, 1), EstadoSolicitud.Entregada, deadline: T(2031, 3, 14));
            return ([pend, cost, desc, entA, fuera, entB],
                    [p.Entregada(entA, T(2031, 3, 15)),   // a tiempo (deadline 20)
                     p.Entregada(entB, T(2031, 3, 16))],  // tarde (deadline 14)
                    []);
        }, async () =>
        {
            var url = $"/api/metricas/cumplimiento?desde={Q(desde)}&hasta={Q(hasta)}";
            var resp = await _fx.Client("AdminTarifas").GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var j = await resp.Json();

            Assert.Equal(desde, j.GetProperty("desde").GetDateTimeOffset());
            Assert.Equal(hasta, j.GetProperty("hasta").GetDateTimeOffset());
            Assert.Equal(4, j.GetProperty("totalSolicitudes").GetInt32());   // pend, cost, desc, entA (no fuera, no entB)
            Assert.Equal(1, j.GetProperty("pendientes").GetInt32());
            Assert.Equal(1, j.GetProperty("enCosteo").GetInt32());
            Assert.Equal(1, j.GetProperty("descartadas").GetInt32());
            Assert.Equal(1, j.GetProperty("entregadas").GetInt32());
            // SLA: entregadas EN el rango con deadline = entA (a tiempo) + entB (tarde).
            Assert.Equal(2, j.GetProperty("entregadasConDeadline").GetInt32());
            Assert.Equal(1, j.GetProperty("entregadasATiempo").GetInt32());
            Assert.Equal(50.0, j.GetProperty("cumplimientoSla").GetDouble());
        });
    }

    [Fact]
    public async Task Solo_desde_o_solo_hasta_tambien_filtran()
    {
        await ConDatos(p => ([p.Solicitud(T(2032, 1, 5), EstadoSolicitud.Pendiente), p.Solicitud(T(2032, 6, 5), EstadoSolicitud.Pendiente)], [], []), async () =>
        {
            var soloDesde = await (await _fx.Client().GetAsync($"/api/metricas/cumplimiento?desde={Q(T(2032, 1, 1))}")).Json();
            var soloHasta = await (await _fx.Client().GetAsync($"/api/metricas/cumplimiento?hasta={Q(T(2032, 12, 31))}")).Json();
            var entre = await (await _fx.Client().GetAsync($"/api/metricas/cumplimiento?desde={Q(T(2032, 1, 1))}&hasta={Q(T(2032, 3, 1))}")).Json();

            Assert.Equal(2, soloDesde.GetProperty("pendientes").GetInt32());   // las 2 (nada más en 2032+)
            Assert.True(soloHasta.GetProperty("pendientes").GetInt32() >= 2);
            Assert.Equal(1, entre.GetProperty("pendientes").GetInt32());
        });
    }

    [Fact]
    public async Task Un_rango_sin_datos_devuelve_ceros_sin_romper_el_porcentaje()
    {
        var j = await (await _fx.Client().GetAsync($"/api/metricas/cumplimiento?desde={Q(T(2040, 1, 1))}&hasta={Q(T(2040, 1, 2))}")).Json();

        Assert.Equal(0, j.GetProperty("totalSolicitudes").GetInt32());
        Assert.Equal(0, j.GetProperty("entregadasConDeadline").GetInt32());
        Assert.Equal(0.0, j.GetProperty("cumplimientoSla").GetDouble());
    }

    // ------------------------------------------------------------ KPIs: tiempo de costeo híbrido

    private async Task<JsonElement> Kpis(DateTimeOffset desde, DateTimeOffset hasta, string? tamano = null)
    {
        var url = $"/api/metricas/kpis-piloto?desde={Q(desde)}&hasta={Q(hasta)}" + (tamano is null ? "" : $"&tamano={tamano}");
        var resp = await _fx.Client("LiderProceso").GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return await resp.Json();
    }

    private static (List<clsSolicitud>, List<clsCotizacion>, List<clsEstadoAutoCosteo>) Escenario(clsSiembra p)
    {
        // A: 100% automático (Completado), 5 líneas: TerminadoEn - SolicitadoEn = 4 min.
        var sa = p.Solicitud(T(2031, 5, 10, 10, 0), EstadoSolicitud.Entregada);
        var a = p.Saga("Completado", sa.Id, T(2031, 5, 10, 10, 0), T(2031, 5, 10, 10, 4), 5);
        // B: con intervención manual (Parcial), 50 líneas. El robot "terminó" al minuto 2, pero la
        // cotización se entregó 30 min después de crearse la solicitud: el ciclo completo son 30.
        var sb = p.Solicitud(T(2031, 5, 11, 9, 0), EstadoSolicitud.Entregada);
        var b = p.Saga("Parcial", sb.Id, T(2031, 5, 11, 9, 1), T(2031, 5, 11, 9, 2), 50);
        var cb = p.Entregada(sb, T(2031, 5, 11, 9, 30));
        // C: Parcial, 200 líneas, TODAVÍA sin entregar: no entra.
        var sc = p.Solicitud(T(2031, 5, 12, 8, 0), EstadoSolicitud.EnCosteo);
        var c = p.Saga("Parcial", sc.Id, T(2031, 5, 12, 8, 0), T(2031, 5, 12, 8, 3), 200);
        // D: Completado, 150 líneas: 20 min.
        var sd = p.Solicitud(T(2031, 5, 13, 8, 0), EstadoSolicitud.Entregada);
        var d = p.Saga("Completado", sd.Id, T(2031, 5, 13, 8, 0), T(2031, 5, 13, 8, 20), 150);
        return ([sa, sb, sc, sd], [cb], [a, b, c, d]);
    }

    private static readonly DateTimeOffset Desde = T(2031, 5, 1);
    private static readonly DateTimeOffset Hasta = T(2031, 5, 31);

    [Fact]
    public async Task Todos_los_tamanos_mezcla_automaticos_y_con_intervencion_manual_y_deja_fuera_el_parcial_sin_entregar()
    {
        await ConDatos(Escenario, async () =>
        {
            var j = await Kpis(Desde, Hasta);

            Assert.Equal(3, j.GetProperty("pliegosResueltos").GetInt32());            // A(4) B(30) D(20); C no
            Assert.Equal(18.0, j.GetProperty("tiempoPromedioResolucionMinutos").GetDouble());
            Assert.Equal(20.0, j.GetProperty("tiempoMedianoResolucionMinutos").GetDouble());
            Assert.Equal(1, j.GetProperty("pliegosBajo10Minutos").GetInt32());
            Assert.Equal(33.3, j.GetProperty("porcentajePliegosBajo10Minutos").GetDouble());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("tamano").ValueKind);
        });
    }

    [Fact]
    public async Task El_pliego_con_intervencion_manual_mide_hasta_la_entrega_no_hasta_que_el_robot_termino()
    {
        await ConDatos(Escenario, async () =>
        {
            var j = await Kpis(Desde, Hasta, "11-100");   // solo B

            Assert.Equal(1, j.GetProperty("pliegosResueltos").GetInt32());
            Assert.Equal(30.0, j.GetProperty("tiempoPromedioResolucionMinutos").GetDouble());   // no 1 minuto
            Assert.Equal(0, j.GetProperty("pliegosBajo10Minutos").GetInt32());
        });
    }

    [Theory]
    [InlineData("1-10", 1, 4.0)]
    [InlineData("11-100", 1, 30.0)]
    [InlineData("%2B100", 1, 20.0)]   // "+100" codificado: C (200 líneas) sigue sin entrar, queda D
    [InlineData("+100", 1, 20.0)]     // "+100" sin codificar llega como espacio: se acepta igual
    public async Task Cada_tamano_corta_por_el_total_de_lineas_de_la_saga(string tamano, int resueltos, double promedio)
    {
        await ConDatos(Escenario, async () =>
        {
            var j = await Kpis(Desde, Hasta, tamano);

            Assert.Equal(resueltos, j.GetProperty("pliegosResueltos").GetInt32());
            Assert.Equal(promedio, j.GetProperty("tiempoPromedioResolucionMinutos").GetDouble());
        });
    }

    [Fact]
    public async Task Un_tamano_sin_pliegos_devuelve_cero_y_promedio_null()
    {
        await ConDatos(p => ([], [], []), async () =>
        {
            var j = await Kpis(T(2036, 1, 1), T(2036, 1, 2), "1-10");

            Assert.Equal(0, j.GetProperty("pliegosResueltos").GetInt32());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("tiempoPromedioResolucionMinutos").ValueKind);
            Assert.Equal(JsonValueKind.Null, j.GetProperty("porcentajePliegosBajo10Minutos").ValueKind);
        });
    }

    [Fact]
    public async Task Un_tamano_invalido_devuelve_400_TAMANO_INVALIDO()
    {
        var resp = await _fx.Client("LiderProceso").GetAsync("/api/metricas/kpis-piloto?tamano=enorme");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("TAMANO_INVALIDO", await resp.ErrorCode());
    }

    [Fact]
    public async Task Los_kpis_sin_parametros_siguen_respondiendo_y_el_rol_Comercial_no_ve_el_tablero()
    {
        Assert.Equal(HttpStatusCode.OK, (await _fx.Client("LiderProceso").GetAsync("/api/metricas/kpis-piloto")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _fx.Client("Comercial").GetAsync("/api/metricas/cumplimiento")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _fx.Client("Comercial").GetAsync("/api/metricas/kpis-piloto")).StatusCode);
    }
}
