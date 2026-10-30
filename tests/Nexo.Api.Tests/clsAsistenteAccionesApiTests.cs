using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Domain.Usuarios.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// Los cambios que propone el asistente, contra la API y la base REALES con el modelo simulado: el modelo solo propone (la cotización no
/// cambia), la persona confirma y se aplica con los casos de uso de siempre, las reglas del dominio mandan y sus rechazos se explican en la
/// conversación, y cada acción queda auditada. Cada prueba crea sus propias personas y su solicitud, y las borra al terminar.
/// </summary>
[Collection("api")]
public class clsAsistenteAccionesApiTests(clsApiFixture fx) : IDisposable
{
    private const string Conversaciones = "/api/asistente/conversaciones";
    private const string Acciones = "/api/asistente/acciones";

    private readonly List<Guid> _usuarios = [];
    private readonly List<Guid> _solicitudes = [];
    private readonly List<WebApplicationFactory<clsProgram>> _fabricas = [];

    // ------------------------------------------------------------ armado

    private Guid NuevoUsuario(RolUsuario rol = RolUsuario.AdminTarifas)
    {
        var id = Guid.NewGuid();
        fx.WithDb(db =>
        {
            db.Usuarios.Add(new clsUsuario
            {
                Id = id, Nombre = "Acciones prueba " + id.ToString("N")[..8], Email = $"acciones-{id:N}@boterosoto.com.co", Rol = rol,
            });
            return db.SaveChanges();
        });
        _usuarios.Add(id);
        return id;
    }

    private HttpClient ClienteDe(WebApplicationFactory<clsProgram> fabrica, string rol, Guid usuario)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.UserIdHeader, usuario.ToString());
        return c;
    }

    private WebApplicationFactory<clsProgram> Fabrica(IConversacionIaPort ia)
    {
        var fabrica = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IConversacionIaPort>();
            s.AddSingleton(ia);
        }));
        _fabricas.Add(fabrica);
        return fabrica;
    }

    /// <summary>Una solicitud con una línea con ruta (sin vehículo ni DANE: no hay piso que verificar) y su cotización en borrador.</summary>
    private async Task<(Guid SolicitudId, Guid CotizacionId, Guid LineaId)> NuevaCotizacion()
    {
        var (solicitudId, lineaId) = fx.WithDb(db =>
        {
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente, CreadoPor = db.Usuarios.First().Id,
            };
            var ruta = new clsRuta { Origen = $"Cali (test373-{Guid.NewGuid():N})"[..30], Destino = $"Bogotá (test373-{Guid.NewGuid():N})"[..30] };
            db.Rutas.Add(ruta);
            var linea = new clsLineaSolicitud { SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, Peso = 1000, NumeroLinea = 1 };
            s.Lineas.Add(linea);
            db.Solicitudes.Add(s);
            db.SaveChanges();
            return (s.Id, linea.Id);
        });
        _solicitudes.Add(solicitudId);

        var resp = await fx.Client().PostJson("/api/cotizaciones", new { solicitudId });
        Assert.True(resp.IsSuccessStatusCode, "No se pudo crear la cotización.");
        return (solicitudId, (await resp.Json()).GetProperty("id").GetGuid(), lineaId);
    }

    private sealed record clsSse(string Nombre, JsonElement Datos);

    private static async Task<List<clsSse>> Enviar(HttpClient c, object cuerpo)
    {
        var resp = await c.PostAsJsonAsync($"{Conversaciones}/mensajes", cuerpo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        List<clsSse> eventos = [];
        foreach (var bloque in (await resp.Content.ReadAsStringAsync()).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var lineas = bloque.Split('\n');
            eventos.Add(new clsSse(lineas[0]["event: ".Length..], JsonDocument.Parse(lineas[1]["data: ".Length..]).RootElement.Clone()));
        }
        return eventos;
    }

    /// <summary>El modelo pide una herramienta de cambio y responde que lo dejó propuesto.</summary>
    private static clsConversacionIaFalsa Propone(string herramienta, object entrada) => new((_, ejecutar, ct) => ProponeYResponde(herramienta, entrada, ejecutar, ct));

    private static async IAsyncEnumerable<clsEventoConversacionIa> ProponeYResponde(
        string herramienta, object entrada, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_1", herramienta, JsonSerializer.SerializeToElement(entrada));
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, await ejecutar(llamada, ct));
        yield return new clsEventoConversacionIa.Texto("Dejé el cambio propuesto; confírmalo en pantalla.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    private static Guid AccionDe(List<clsSse> eventos) => eventos.Single(e => e.Nombre == "accion").Datos.GetProperty("id").GetGuid();

    private async Task<JsonElement> LineaDeLaMesa(Guid solicitudId, Guid lineaId)
    {
        var mesa = await fx.Client("AdminTarifas").GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}");
        return mesa.GetProperty("lineas").EnumerateArray().Single(l => l.GetProperty("lineaId").GetGuid() == lineaId);
    }

    public void Dispose()
    {
        foreach (var f in _fabricas) f.Dispose();
        try
        {
            fx.WithDb(db =>
            {
                var conversaciones = db.ConversacionesAsistente.Where(c => _usuarios.Contains(c.UsuarioId)).Select(c => c.Id).ToList();
                db.AccionesAsistente.Where(a => conversaciones.Contains(a.ConversacionId)).ExecuteDelete();
                db.UsosHerramientaAsistente.Where(h => conversaciones.Contains(h.ConversacionId)).ExecuteDelete();
                db.MensajesAsistente.Where(m => conversaciones.Contains(m.ConversacionId)).ExecuteDelete();
                db.ConversacionesAsistente.Where(c => _usuarios.Contains(c.UsuarioId)).ExecuteDelete();
                db.UsosIa.Where(u => u.UsuarioId != null && _usuarios.Contains(u.UsuarioId.Value)).ExecuteDelete();
                db.AuditLogs.Where(a => a.UsuarioId != null && _usuarios.Contains(a.UsuarioId.Value)).ExecuteDelete();
                foreach (var solicitudId in _solicitudes)
                {
                    db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                    db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                    db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                }
                db.Rutas.Where(r => r.Origen.Contains("(test373-") && !db.LineasSolicitud.Any(l => l.RutaId == r.Id)).ExecuteDelete();
                db.Usuarios.Where(u => _usuarios.Contains(u.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Mejor esfuerzo: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba ya correcta.
        }
    }

    // ------------------------------------------------------------ el modelo solo propone; la persona confirma

    [Fact]
    public async Task Proponer_rechazar_una_linea_no_la_toca_y_confirmarlo_la_rechaza_y_lo_deja_en_la_conversacion()
    {
        var yo = NuevoUsuario();
        var (solicitudId, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_rechazar_linea", new { cotizacionId, lineaSolicitudId = lineaId, motivo = "Sin tarifa vigente" })), "AdminTarifas", yo);

        var eventos = await Enviar(c, new { mensaje = "Rechaza esa línea: no hay tarifa" });

        // lo propuesto llega al cliente y nada cambió todavía
        var accion = eventos.Single(e => e.Nombre == "accion").Datos;
        Assert.Equal("rechazar_linea", accion.GetProperty("tipo").GetString());
        Assert.Equal("pendiente", accion.GetProperty("estado").GetString());
        Assert.Contains("Sin tarifa vigente", accion.GetProperty("resumen").GetString());
        Assert.Equal("porCostear", (await LineaDeLaMesa(solicitudId, lineaId)).GetProperty("estadoLinea").GetString());

        var confirmada = await c.PostAsync($"{Acciones}/{AccionDe(eventos)}/confirmar", null);

        Assert.Equal(HttpStatusCode.OK, confirmada.StatusCode);
        var cuerpo = await confirmada.Json();
        Assert.Equal("ejecutada", cuerpo.GetProperty("estado").GetString());
        Assert.Equal("ok", cuerpo.GetProperty("resultadoCodigo").GetString());
        var linea = await LineaDeLaMesa(solicitudId, lineaId);
        Assert.Equal("rechazada", linea.GetProperty("estadoLinea").GetString());
        Assert.Equal("Sin tarifa vigente", linea.GetProperty("motivoRechazo").GetString());

        // queda en la conversación (la acción con su resultado y el aviso del asistente) para auditar
        var conversacionId = eventos.Single(e => e.Nombre == "conversacion").Datos.GetProperty("conversacionId").GetGuid();
        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Conversaciones}/{conversacionId}");
        Assert.Equal("ejecutada", detalle.GetProperty("acciones").EnumerateArray().Single().GetProperty("estado").GetString());
        Assert.StartsWith("Listo, apliqué el cambio", detalle.GetProperty("mensajes").EnumerateArray().Last().GetProperty("contenido").GetString());
        Assert.Equal("proponer_rechazar_linea", detalle.GetProperty("herramientas").EnumerateArray().Single().GetProperty("herramienta").GetString());
    }

    [Fact]
    public async Task Un_flete_con_el_valor_que_escribio_la_persona_se_propone_y_al_confirmar_queda_en_la_cotizacion()
    {
        var yo = NuevoUsuario();
        var (_, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_agregar_flete",
            new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = 4_500_000, fuente = "Manual" })), "AdminTarifas", yo);

        var eventos = await Enviar(c, new { mensaje = "Agrega a esa línea un flete manual de $4.500.000" });
        var accionId = AccionDe(eventos);

        var antes = await fx.Client().GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cotizacionId}");
        Assert.Equal(0, antes.GetProperty("lineas").GetArrayLength());

        var confirmada = await c.PostAsync($"{Acciones}/{accionId}/confirmar", null);

        Assert.Equal(HttpStatusCode.OK, confirmada.StatusCode);
        var despues = await fx.Client().GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cotizacionId}");
        Assert.Equal(4_500_000m, despues.GetProperty("total").GetDecimal());
        Assert.Equal(lineaId, despues.GetProperty("lineas")[0].GetProperty("lineaSolicitudId").GetGuid());
    }

    [Fact]
    public async Task Un_valor_que_no_salio_de_ninguna_consulta_no_se_propone_y_queda_auditado_el_rechazo()
    {
        var yo = NuevoUsuario();
        var (_, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_agregar_flete",
            new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = 9_999_999, fuente = "Manual" })), "AdminTarifas", yo);

        var eventos = await Enviar(c, new { mensaje = "Ponle un flete a esa línea, tú sabrás cuánto" });

        Assert.DoesNotContain(eventos, e => e.Nombre == "accion");
        Assert.Equal(["ejecutando", "error"], eventos.Where(e => e.Nombre == "herramienta").Select(e => e.Datos.GetProperty("estado").GetString()));
        var conversacionId = eventos.Single(e => e.Nombre == "conversacion").Datos.GetProperty("conversacionId").GetGuid();
        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Conversaciones}/{conversacionId}");
        Assert.Empty(detalle.GetProperty("acciones").EnumerateArray());
        Assert.Equal("VALOR_SIN_ORIGEN", detalle.GetProperty("herramientas").EnumerateArray().Single().GetProperty("resultado").GetString());
    }

    // ------------------------------------------------------------ las reglas del dominio mandan

    [Fact]
    public async Task Un_rechazo_del_dominio_se_responde_con_su_codigo_no_cambia_nada_y_la_conversacion_lo_explica()
    {
        var yo = NuevoUsuario();
        var (_, cotizacionId, lineaId) = await NuevaCotizacion();
        // Editar el flete de una línea que no tiene flete: el dominio lo rechaza (404 LINEA_NO_ENCONTRADA) al confirmar.
        var c = ClienteDe(Fabrica(Propone("proponer_editar_flete",
            new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = 3_000_000 })), "AdminTarifas", yo);
        var eventos = await Enviar(c, new { mensaje = "Cambia el flete de esa línea a $3.000.000" });

        var confirmada = await c.PostAsync($"{Acciones}/{AccionDe(eventos)}/confirmar", null);

        Assert.Equal(HttpStatusCode.NotFound, confirmada.StatusCode);
        Assert.Equal("LINEA_NO_ENCONTRADA", await confirmada.ErrorCode());
        var conversacionId = eventos.Single(e => e.Nombre == "conversacion").Datos.GetProperty("conversacionId").GetGuid();
        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Conversaciones}/{conversacionId}");
        var accion = detalle.GetProperty("acciones").EnumerateArray().Single();
        Assert.Equal("fallida", accion.GetProperty("estado").GetString());
        Assert.Equal("LINEA_NO_ENCONTRADA", accion.GetProperty("resultadoCodigo").GetString());
        var aviso = detalle.GetProperty("mensajes").EnumerateArray().Last().GetProperty("contenido").GetString()!;
        Assert.Contains("LINEA_NO_ENCONTRADA", aviso);
        Assert.Contains("No se modificó nada", aviso);
        var cotizacion = await fx.Client().GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cotizacionId}");
        Assert.Equal(0, cotizacion.GetProperty("total").GetDecimal());
    }

    // ------------------------------------------------------------ quién, cuántas veces, hasta cuándo

    [Fact]
    public async Task Solo_quien_tiene_la_conversacion_confirma_y_una_accion_se_confirma_una_sola_vez()
    {
        var yo = NuevoUsuario();
        var otro = NuevoUsuario();
        var (solicitudId, cotizacionId, lineaId) = await NuevaCotizacion();
        var fabrica = Fabrica(Propone("proponer_rechazar_linea", new { cotizacionId, lineaSolicitudId = lineaId, motivo = "Sin tarifa" }));
        var mia = ClienteDe(fabrica, "AdminTarifas", yo);
        var accionId = AccionDe(await Enviar(mia, new { mensaje = "rechaza la línea" }));

        var deOtro = await ClienteDe(fabrica, "AdminTarifas", otro).PostAsync($"{Acciones}/{accionId}/confirmar", null);
        var deAdmin = await ClienteDe(fabrica, "Admin", NuevoUsuario(RolUsuario.Admin)).PostAsync($"{Acciones}/{accionId}/confirmar", null);
        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (deOtro.StatusCode, deAdmin.StatusCode));
        Assert.Equal("ACCION_AJENA", await deOtro.ErrorCode());
        Assert.Equal("porCostear", (await LineaDeLaMesa(solicitudId, lineaId)).GetProperty("estadoLinea").GetString());

        Assert.Equal(HttpStatusCode.OK, (await mia.PostAsync($"{Acciones}/{accionId}/confirmar", null)).StatusCode);
        var otraVez = await mia.PostAsync($"{Acciones}/{accionId}/confirmar", null);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        Assert.Equal("ACCION_YA_RESUELTA", await otraVez.ErrorCode());
    }

    [Fact]
    public async Task Dos_confirmaciones_a_la_vez_aplican_el_cambio_una_sola_vez()
    {
        var yo = NuevoUsuario();
        var (_, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_agregar_flete",
            new { cotizacionId, lineaSolicitudId = lineaId, valorFlete = 4_500_000, fuente = "Manual" })), "AdminTarifas", yo);
        var accionId = AccionDe(await Enviar(c, new { mensaje = "Agrega un flete manual de $4.500.000" }));

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => c.PostAsync($"{Acciones}/{accionId}/confirmar", null)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        var cotizacion = await fx.Client().GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cotizacionId}");
        Assert.Equal(1, cotizacion.GetProperty("lineas").GetArrayLength());
        Assert.Equal(4_500_000m, cotizacion.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Una_accion_vencida_no_se_ejecuta()
    {
        var yo = NuevoUsuario();
        var (solicitudId, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_rechazar_linea", new { cotizacionId, lineaSolicitudId = lineaId, motivo = "Sin tarifa" })), "AdminTarifas", yo);
        var accionId = AccionDe(await Enviar(c, new { mensaje = "rechaza la línea" }));
        fx.WithDb(db => db.AccionesAsistente.Where(a => a.Id == accionId).ExecuteUpdate(s => s.SetProperty(a => a.VenceAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        var resp = await c.PostAsync($"{Acciones}/{accionId}/confirmar", null);

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("ACCION_VENCIDA", await resp.ErrorCode());
        Assert.Equal("porCostear", (await LineaDeLaMesa(solicitudId, lineaId)).GetProperty("estadoLinea").GetString());
        Assert.Equal("vencida", fx.WithDb(db => db.AccionesAsistente.Single(a => a.Id == accionId).Estado));
    }

    [Fact]
    public async Task Una_accion_cancelada_no_se_aplica_y_ya_no_se_puede_confirmar()
    {
        var yo = NuevoUsuario();
        var (solicitudId, cotizacionId, lineaId) = await NuevaCotizacion();
        var c = ClienteDe(Fabrica(Propone("proponer_rechazar_linea", new { cotizacionId, lineaSolicitudId = lineaId, motivo = "Sin tarifa" })), "AdminTarifas", yo);
        var accionId = AccionDe(await Enviar(c, new { mensaje = "rechaza la línea" }));

        var cancelada = await c.PostAsync($"{Acciones}/{accionId}/cancelar", null);
        var confirmar = await c.PostAsync($"{Acciones}/{accionId}/confirmar", null);

        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        Assert.Equal("cancelada", (await cancelada.Json()).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Conflict, confirmar.StatusCode);
        Assert.Equal("porCostear", (await LineaDeLaMesa(solicitudId, lineaId)).GetProperty("estadoLinea").GetString());
    }

    [Fact]
    public async Task Una_accion_inexistente_es_404_y_solo_AdminTarifas_y_Admin_llegan_a_los_endpoints()
    {
        var yo = NuevoUsuario();
        var fabrica = Fabrica(Propone("proponer_rechazar_linea", new { }));
        var inexistente = Guid.NewGuid();

        foreach (var rol in new[] { "AdminTarifas", "Admin" })
        {
            var c = ClienteDe(fabrica, rol, yo);
            Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"{Acciones}/{inexistente}/confirmar", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"{Acciones}/{inexistente}/cancelar", null)).StatusCode);
        }
        foreach (var rol in new[] { "Comercial", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            var c = ClienteDe(fabrica, rol, yo);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync($"{Acciones}/{inexistente}/confirmar", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync($"{Acciones}/{inexistente}/cancelar", null)).StatusCode);
        }
        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync($"{Acciones}/{inexistente}/confirmar", null)).StatusCode);
    }
}
