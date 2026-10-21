using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-24.3/#320 contra la API y la base reales: rechazar una línea de la cotización con un motivo, deshacerlo, entregar con líneas
/// rechazadas (también con todas rechazadas), el total, el Excel («Sin costeo», sin precio ni motivo) y los roles.
/// Usa las columnas de #314 (MotivoRechazo, RechazadaPor, RechazadaAt) de tbl_lineas_solicitud.
/// </summary>
[Collection("api")]
public class clsRechazoLineaCotizacionApiTests
{
    private readonly clsApiFixture _fx;
    public clsRechazoLineaCotizacionApiTests(clsApiFixture fx) => _fx = fx;

    private static readonly string[] SinPermiso = ["Comercial", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso"];

    /// <summary>Una solicitud con <paramref name="n"/> líneas con ruta (sin vehículo ni DANE: no hay piso que verificar).</summary>
    private (Guid SolicitudId, Guid[] Lineas) NuevaSolicitud(int n) => _fx.WithDb(db =>
    {
        var s = new clsSolicitud
        {
            ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
            CreadoPor = db.Usuarios.First().Id,
        };
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        for (var i = 1; i <= n; i++)
        {
            var ruta = new clsRuta { Origen = $"Cali (test320-{sufijo}-{i})", Destino = $"Bogotá (test320-{sufijo}-{i})" };
            db.Rutas.Add(ruta);
            s.Lineas.Add(new clsLineaSolicitud { SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, Peso = 1000 + i, NumeroLinea = i });
        }
        db.Solicitudes.Add(s);
        db.SaveChanges();
        return (s.Id, s.Lineas.OrderBy(l => l.NumeroLinea).Select(l => l.Id).ToArray());
    });

    private async Task<Guid> NuevaCotizacion(HttpClient cli, Guid solicitudId)
    {
        var resp = await cli.PostJson("/api/cotizaciones", new { solicitudId });
        Assert.True(resp.IsSuccessStatusCode, "No se pudo crear la cotización.");
        return (await resp.Json()).GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> Rechazar(HttpClient cli, Guid cot, Guid lin, string? motivo) =>
        await cli.PostJson($"/api/cotizaciones/{cot}/lineas/{lin}/rechazo", new { motivo });

    private static Task<HttpResponseMessage> Deshacer(HttpClient cli, Guid cot, Guid lin) =>
        cli.DeleteAsync($"/api/cotizaciones/{cot}/lineas/{lin}/rechazo");

    private static async Task AgregarFlete(HttpClient cli, Guid cot, Guid lin, decimal valor)
    {
        var r = await cli.PostJson($"/api/cotizaciones/{cot}/lineas", new { lineaSolicitudId = lin, valorFlete = valor, fuente = "manual" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    private void Limpiar(Guid solicitudId)
    {
        // Mejor esfuerzo: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba ya correcta.
        try
        {
            _fx.WithDb(db =>
            {
                db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                // Las rutas de prueba también (llevan la marca "(test320-"): sin esto cada corrida dejaba rutas sueltas en la base compartida.
                db.Rutas.Where(r => r.Origen.Contains("(test320-") && !db.LineasSolicitud.Any(l => l.RutaId == r.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }

    private async Task<JsonElement> LineaDeLaMesa(Guid solicitudId, Guid lineaId)
    {
        var mesa = await _fx.Client("AdminTarifas").GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}");
        return mesa.GetProperty("lineas").EnumerateArray().Single(l => l.GetProperty("lineaId").GetGuid() == lineaId);
    }

    // ------------------------------------------------------------------ roles

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminBiblioteca")]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task Los_demas_roles_reciben_403_al_rechazar_y_al_deshacer(string rol)
    {
        var cli = _fx.Client(rol);
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await Rechazar(cli, id, id, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Deshacer(cli, id, id)).StatusCode);
    }

    [Theory]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task AdminTarifas_y_Admin_pueden_rechazar_y_deshacer(string rol)
    {
        var (solicitudId, lineas) = NuevaSolicitud(1);
        try
        {
            var cot = await NuevaCotizacion(_fx.Client(), solicitudId);
            var cli = _fx.Client(rol);

            var rechazo = await Rechazar(cli, cot, lineas[0], "Sin tarifa vigente");
            Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
            var cuerpo = await rechazo.Json();
            Assert.Equal("rechazada", cuerpo.GetProperty("estadoLinea").GetString());
            Assert.Equal("Sin tarifa vigente", cuerpo.GetProperty("motivoRechazo").GetString());

            var deshacer = await Deshacer(cli, cot, lineas[0]);
            Assert.Equal(HttpStatusCode.OK, deshacer.StatusCode);
            Assert.Equal("porCostear", (await deshacer.Json()).GetProperty("estadoLinea").GetString());
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ rechazar

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Sin_motivo_responde_400(string? motivo)
    {
        var (solicitudId, lineas) = NuevaSolicitud(1);
        try
        {
            var cot = await NuevaCotizacion(_fx.Client(), solicitudId);

            var resp = await Rechazar(_fx.Client("AdminTarifas"), cot, lineas[0], motivo);

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal(EstadoLinea.PorCostear, _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.Id == lineas[0]).EstadoLinea));
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task El_rechazo_queda_guardado_con_quien_y_cuando_y_la_Mesa_ve_estado_y_motivo()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var cot = await NuevaCotizacion(_fx.Client(), solicitudId);
            Assert.Equal(HttpStatusCode.OK, (await Rechazar(_fx.Client("AdminTarifas"), cot, lineas[1], "El cliente cambió la carga")).StatusCode);

            var fila = _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.Id == lineas[1]));
            Assert.Equal(EstadoLinea.Rechazada, fila.EstadoLinea);
            Assert.Equal("El cliente cambió la carga", fila.MotivoRechazo);
            Assert.NotNull(fila.RechazadaPor);
            Assert.NotNull(fila.RechazadaAt);

            // GET /api/solicitudes/{id}: estado y motivo por línea, opcionales (la otra línea los trae null).
            var rechazada = await LineaDeLaMesa(solicitudId, lineas[1]);
            Assert.Equal("rechazada", rechazada.GetProperty("estadoLinea").GetString());
            Assert.Equal("El cliente cambió la carga", rechazada.GetProperty("motivoRechazo").GetString());
            var otra = await LineaDeLaMesa(solicitudId, lineas[0]);
            Assert.Equal(JsonValueKind.Null, otra.GetProperty("motivoRechazo").ValueKind);
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Una_linea_con_flete_no_se_puede_rechazar_409_y_una_ya_rechazada_tampoco()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            await AgregarFlete(admin, cot, lineas[0], 1_000_000m);

            var conFlete = await Rechazar(admin, cot, lineas[0], "x");
            Assert.Equal(HttpStatusCode.Conflict, conFlete.StatusCode);
            Assert.Equal("LINEA_CON_FLETE", await conFlete.ErrorCode());

            Assert.Equal(HttpStatusCode.OK, (await Rechazar(admin, cot, lineas[1], "primero")).StatusCode);
            var repetida = await Rechazar(admin, cot, lineas[1], "otra vez");
            Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
            Assert.Equal("LINEA_YA_RECHAZADA", await repetida.ErrorCode());
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ entregar, total y deshacer

    [Fact]
    public async Task Entrega_con_una_linea_rechazada_el_total_no_la_suma_y_despues_ya_no_se_puede_deshacer()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            await AgregarFlete(admin, cot, lineas[0], 1_000_000m);
            Assert.Equal(HttpStatusCode.OK, (await Rechazar(admin, cot, lineas[1], "Sin tarifa")).StatusCode);

            var entrega = await admin.PostAsync($"/api/cotizaciones/{cot}/entregar", null);

            Assert.Equal(HttpStatusCode.OK, entrega.StatusCode);
            var dto = (await entrega.Json()).GetProperty("cotizacion");
            Assert.Equal(1_000_000m, dto.GetProperty("total").GetDecimal());          // la rechazada no suma
            Assert.Equal(1, dto.GetProperty("lineas").GetArrayLength());              // y no tiene fila de cotización

            // Deshacer solo antes de entregar: entregada → 409.
            var deshacer = await Deshacer(admin, cot, lineas[1]);
            Assert.Equal(HttpStatusCode.Conflict, deshacer.StatusCode);
            Assert.Equal("COTIZACION_NO_EDITABLE", await deshacer.ErrorCode());
            Assert.Equal(EstadoLinea.Rechazada, _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.Id == lineas[1]).EstadoLinea));
        }
        finally { Limpiar(solicitudId); }
    }

    // Todas rechazadas: no hay ningún flete (hoy lo impide la regla de al menos uno) y se entrega igual, con los motivos.
    [Fact]
    public async Task Con_todas_las_lineas_rechazadas_la_cotizacion_se_entrega_con_total_cero()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            foreach (var l in lineas) Assert.Equal(HttpStatusCode.OK, (await Rechazar(admin, cot, l, "Sin tarifa")).StatusCode);

            var entrega = await admin.PostAsync($"/api/cotizaciones/{cot}/entregar", null);

            Assert.Equal(HttpStatusCode.OK, entrega.StatusCode);
            Assert.Equal(0m, (await entrega.Json()).GetProperty("cotizacion").GetProperty("total").GetDecimal());
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Una_pendiente_mas_una_rechazada_siguen_sin_poder_entregarse_y_deshacer_la_devuelve_a_por_costear()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            Assert.Equal(HttpStatusCode.OK, (await Rechazar(admin, cot, lineas[1], "Sin tarifa")).StatusCode);

            // lineas[0] sigue PorCostear: no se puede entregar.
            var entrega = await admin.PostAsync($"/api/cotizaciones/{cot}/entregar", null);
            Assert.Equal(HttpStatusCode.Conflict, entrega.StatusCode);
            Assert.Equal("COTIZACION_CON_LINEAS_PENDIENTES", await entrega.ErrorCode());

            // Deshacer: la línea vuelve a PorCostear, sin motivo de rechazo.
            Assert.Equal(HttpStatusCode.OK, (await Deshacer(admin, cot, lineas[1])).StatusCode);
            var linea = await LineaDeLaMesa(solicitudId, lineas[1]);
            Assert.Equal("porCostear", linea.GetProperty("estadoLinea").GetString());
            Assert.Equal(JsonValueKind.Null, linea.GetProperty("motivoRechazo").ValueKind);
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ Excel

    [Fact]
    public async Task El_Excel_muestra_la_linea_rechazada_como_Sin_costeo_sin_precio_y_sin_motivo()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            await AgregarFlete(admin, cot, lineas[0], 1_000_000m);
            const string motivo = "Motivo-secreto-del-rechazo";
            Assert.Equal(HttpStatusCode.OK, (await Rechazar(admin, cot, lineas[1], motivo)).StatusCode);

            var resp = await admin.GetAsync($"/api/cotizaciones/{cot}/exportar-excel");

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            using var wb = new XLWorkbook(await resp.Content.ReadAsStreamAsync());
            var ws = wb.Worksheet(1);
            var celdas = ws.CellsUsed().ToList();
            var sinCosteo = celdas.Single(c => c.GetString() == "Sin costeo");
            // la fila de la rechazada: su ruta y «Sin costeo» en la columna del flete; ningún número.
            Assert.Contains("Cali", ws.Cell(sinCosteo.Address.RowNumber, 1).GetString());
            Assert.Equal(XLDataType.Text, sinCosteo.DataType);
            // El motivo NO va en el Excel (decisión: solo en el correo al comercial).
            Assert.DoesNotContain(celdas, c => c.GetString().Contains(motivo));
            // El total es solo el del flete costeado.
            var total = celdas.Single(c => c.GetString() == "TOTAL COTIZACIÓN");
            Assert.Equal(1_000_000m, ws.Cell(total.Address.RowNumber, 3).GetValue<decimal>());
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task El_Excel_de_una_cotizacion_con_todas_las_lineas_rechazadas_sale_con_todas_en_Sin_costeo()
    {
        var (solicitudId, lineas) = NuevaSolicitud(2);
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var cot = await NuevaCotizacion(admin, solicitudId);
            foreach (var l in lineas) await Rechazar(admin, cot, l, "Sin tarifa");

            var resp = await admin.GetAsync($"/api/cotizaciones/{cot}/exportar-excel");

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            using var wb = new XLWorkbook(await resp.Content.ReadAsStreamAsync());
            Assert.Equal(2, wb.Worksheet(1).CellsUsed().Count(c => c.GetString() == "Sin costeo"));
        }
        finally { Limpiar(solicitudId); }
    }
}
