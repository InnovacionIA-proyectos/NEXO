using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Asistente.Conversacion;
using Nexo.Application.CasosDeUso.IA.Interfaces;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Domain.Usuarios.Entidades;
using Nexo.Infrastructure.IA;

namespace Nexo.Api.Tests;

/// <summary>
/// La conversación con el asistente contra la API y la base REALES, con el modelo simulado (ninguna prueba llama a Anthropic): quién
/// puede usarla, la respuesta en tiempo real, que lo guardado y lo auditado sea lo ocurrido, que un precio sin origen no se entregue como
/// bueno, el corte por límite de turnos y por tope diario, y el interruptor de apagado. Cada prueba trabaja con personas propias que se
/// borran, con todo lo que crearon, al terminar.
/// </summary>
[Collection("api")]
public class clsAsistenteConversacionApiTests(clsApiFixture fx) : IDisposable
{
    private const string Ruta = "/api/asistente/conversaciones";

    private readonly List<Guid> _usuarios = [];
    private readonly List<WebApplicationFactory<clsProgram>> _fabricas = [];

    // ------------------------------------------------------------ armado

    private Guid NuevoUsuario(RolUsuario rol = RolUsuario.AdminTarifas)
    {
        var id = Guid.NewGuid();
        fx.WithDb(db =>
        {
            db.Usuarios.Add(new clsUsuario
            {
                Id = id, Nombre = "Asistente prueba " + id.ToString("N")[..8], Email = $"asistente-{id:N}@boterosoto.com.co", Rol = rol,
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

    private WebApplicationFactory<clsProgram> Fabrica(
        IConversacionIaPort ia, Action<IServiceCollection>? ajustar = null, bool conRegistro = false)
    {
        var fabrica = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IConversacionIaPort>();
            if (conRegistro)
                s.AddScoped<IConversacionIaPort>(sp => new clsConversacionIaConRegistro(
                    ia, sp.GetRequiredService<IUsoIaRepository>(),
                    Options.Create(new clsOpcionesAnthropic { LimiteTokensPorUsuarioDia = 1000, Model = "claude-sonnet-5-5" }),
                    TimeProvider.System, NullLogger<clsConversacionIaConRegistro>.Instance));
            else
                s.AddSingleton(ia);
            ajustar?.Invoke(s);
        }));
        _fabricas.Add(fabrica);
        return fabrica;
    }

    private static clsConversacionIaFalsa Modelo(
        Func<clsPeticionConversacionIa, Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>>, CancellationToken, IAsyncEnumerable<clsEventoConversacionIa>> guion) =>
        new(guion);

    private static async IAsyncEnumerable<clsEventoConversacionIa> Dice(string texto, int entrada = 100, int salida = 20)
    {
        yield return new clsEventoConversacionIa.Texto(texto);
        yield return new clsEventoConversacionIa.Uso(new clsUsoDeTokens("claude-sonnet-5-5", entrada, salida, 0, 0));
        yield return new clsEventoConversacionIa.Fin("end_turn");
        await Task.CompletedTask;
    }

    /// <summary>Un evento del flujo <c>text/event-stream</c>.</summary>
    private sealed record clsSse(string Nombre, JsonElement Datos);

    private static async Task<(HttpStatusCode Estado, string? Tipo, List<clsSse> Eventos)> Enviar(HttpClient c, object cuerpo)
    {
        var resp = await c.PostAsJsonAsync($"{Ruta}/mensajes", cuerpo);
        var texto = await resp.Content.ReadAsStringAsync();
        List<clsSse> eventos = [];
        if (resp.Content.Headers.ContentType?.MediaType == "text/event-stream")
            foreach (var bloque in texto.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                var lineas = bloque.Split('\n');
                eventos.Add(new clsSse(lineas[0]["event: ".Length..], JsonDocument.Parse(lineas[1]["data: ".Length..]).RootElement.Clone()));
            }
        return (resp.StatusCode, resp.Content.Headers.ContentType?.MediaType, eventos);
    }

    private static Guid ConversacionDe(List<clsSse> eventos) =>
        eventos.Single(e => e.Nombre == "conversacion").Datos.GetProperty("conversacionId").GetGuid();

    public void Dispose()
    {
        foreach (var f in _fabricas) f.Dispose();
        fx.WithDb(db =>
        {
            db.UsosHerramientaAsistente.Where(h => db.ConversacionesAsistente.Any(c => c.Id == h.ConversacionId && _usuarios.Contains(c.UsuarioId))).ExecuteDelete();
            db.MensajesAsistente.Where(m => db.ConversacionesAsistente.Any(c => c.Id == m.ConversacionId && _usuarios.Contains(c.UsuarioId))).ExecuteDelete();
            db.ConversacionesAsistente.Where(c => _usuarios.Contains(c.UsuarioId)).ExecuteDelete();
            db.UsosIa.Where(u => u.UsuarioId != null && _usuarios.Contains(u.UsuarioId.Value)).ExecuteDelete();
            db.Usuarios.Where(u => _usuarios.Contains(u.Id)).ExecuteDelete();
            return 0;
        });
    }

    // ------------------------------------------------------------ roles

    [Fact]
    public async Task Solo_AdminTarifas_y_Admin_conversan_y_sin_sesion_es_401()
    {
        var yo = NuevoUsuario();
        var fabrica = Fabrica(Modelo((_, _, _) => Dice("Hola")));

        foreach (var rol in new[] { "AdminTarifas", "Admin" })
            Assert.Equal(HttpStatusCode.OK, (await Enviar(ClienteDe(fabrica, rol, yo), new { mensaje = "hola" })).Estado);

        foreach (var rol in new[] { "Comercial", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            var c = ClienteDe(fabrica, rol, yo);
            Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(c, new { mensaje = "hola" })).Estado);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Ruta)).StatusCode);
        }

        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync($"{Ruta}/mensajes", new { mensaje = "hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(Ruta)).StatusCode);
    }

    // ------------------------------------------------------------ la respuesta

    [Fact]
    public async Task La_respuesta_llega_como_eventos_en_orden_y_queda_guardada()
    {
        var yo = NuevoUsuario();
        var fabrica = Fabrica(Modelo((_, _, _) => Dice("Hola, ¿en qué te ayudo?")));
        var c = ClienteDe(fabrica, "AdminTarifas", yo);

        var (estado, tipo, eventos) = await Enviar(c, new { mensaje = "Hola" });

        Assert.Equal(HttpStatusCode.OK, estado);
        Assert.Equal("text/event-stream", tipo);
        Assert.Equal(["conversacion", "texto", "fin"], eventos.Select(e => e.Nombre));
        Assert.Equal("Hola, ¿en qué te ayudo?", eventos[1].Datos.GetProperty("delta").GetString());
        Assert.Equal("completo", eventos[2].Datos.GetProperty("estado").GetString());

        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Ruta}/{ConversacionDe(eventos)}");
        Assert.Equal("Hola", detalle.GetProperty("titulo").GetString());
        Assert.Equal(1, detalle.GetProperty("turnos").GetInt32());
        var mensajes = detalle.GetProperty("mensajes").EnumerateArray().ToList();
        Assert.Equal(["user", "assistant"], mensajes.Select(m => m.GetProperty("rol").GetString()));
        Assert.Equal("Hola, ¿en qué te ayudo?", mensajes[1].GetProperty("contenido").GetString());
        Assert.Equal("completo", mensajes[1].GetProperty("estado").GetString());
    }

    [Theory]
    [InlineData("{}", "MENSAJE_REQUERIDO")]
    [InlineData("{\"mensaje\":\"   \"}", "MENSAJE_REQUERIDO")]
    public async Task Un_mensaje_vacio_responde_400_antes_de_abrir_el_flujo(string cuerpo, string codigo)
    {
        var yo = NuevoUsuario();
        var c = ClienteDe(Fabrica(Modelo((_, _, _) => Dice("Hola"))), "AdminTarifas", yo);

        var resp = await c.PostAsync($"{Ruta}/mensajes", new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(codigo, await resp.ErrorCode());
        Assert.Empty((await c.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray());
    }

    [Fact]
    public async Task El_modelo_recibe_solo_las_herramientas_del_rol_y_cada_persona_ve_solo_sus_conversaciones()
    {
        var yo = NuevoUsuario();
        var otro = NuevoUsuario();
        var ia = Modelo((_, _, _) => Dice("Hola"));
        var fabrica = Fabrica(ia);
        var mia = ClienteDe(fabrica, "AdminTarifas", yo);
        var ajena = ClienteDe(fabrica, "AdminTarifas", otro);

        var (_, _, eventos) = await Enviar(mia, new { mensaje = "mi conversación" });
        await Enviar(ajena, new { mensaje = "la de otra persona" });

        var herramientas = ia.Peticiones[0].Herramientas.Select(h => h.Nombre).ToList();
        Assert.Contains("consultar_tarifa_plaza_nexo", herramientas);
        Assert.DoesNotContain("consultar_aprovisionamiento", herramientas); // solo Comercial y Admin
        var lista = await mia.GetFromJsonAsync<JsonElement>(Ruta);
        Assert.Equal(["mi conversación"], lista.EnumerateArray().Select(c => c.GetProperty("titulo").GetString()));

        // otra persona no lee ni escribe en la mía; el Admin la lee pero no escribe en ella
        var id = ConversacionDe(eventos);
        Assert.Equal(HttpStatusCode.Forbidden, (await ajena.GetAsync($"{Ruta}/{id}")).StatusCode);
        var escritura = await ajena.PostAsJsonAsync($"{Ruta}/mensajes", new { conversacionId = id, mensaje = "me cuelo" });
        Assert.Equal(HttpStatusCode.Forbidden, escritura.StatusCode);
        Assert.Equal("CONVERSACION_AJENA", await escritura.ErrorCode());
        var admin = ClienteDe(fabrica, "Admin", NuevoUsuario(RolUsuario.Admin));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{Ruta}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{Ruta}/mensajes", new { conversacionId = id, mensaje = "yo también" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await mia.GetAsync($"{Ruta}/{Guid.NewGuid()}")).StatusCode);
    }

    // ------------------------------------------------------------ precios y herramientas

    /// <summary>El modelo pide una herramienta real y responde con un precio: el de la herramienta, o uno propio.</summary>
    private static clsConversacionIaFalsa PideConexosYCita(bool citaElDeLaHerramienta) => Modelo((_, ejecutar, ct) => Guion(ejecutar, citaElDeLaHerramienta, ct));

    private static async IAsyncEnumerable<clsEventoConversacionIa> Guion(
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, bool cita, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_1", "consultar_servicios_conexos", JsonDocument.Parse("""{"top":1}""").RootElement.Clone());
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        var resultado = await ejecutar(llamada, ct);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, resultado);

        string precio;
        if (cita)
        {
            using var doc = JsonDocument.Parse(resultado.Contenido);
            var costo = doc.RootElement.GetProperty("valor").GetProperty("items")[0].GetProperty("costoUnitario").GetDecimal();
            precio = costo.ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
        }
        else
            precio = "7.777.777";
        yield return new clsEventoConversacionIa.Texto($"El costo unitario es ${precio}.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task Una_herramienta_real_queda_auditada_y_el_precio_que_cita_viene_de_ella()
    {
        var yo = NuevoUsuario();
        var c = ClienteDe(Fabrica(PideConexosYCita(citaElDeLaHerramienta: true)), "AdminTarifas", yo);

        var (_, _, eventos) = await Enviar(c, new { mensaje = "¿cuánto cuesta un conexo?" });

        Assert.Equal(["ejecutando", "ok"], eventos.Where(e => e.Nombre == "herramienta").Select(e => e.Datos.GetProperty("estado").GetString()));
        Assert.Equal("completo", eventos.Last().Datos.GetProperty("estado").GetString());
        Assert.DoesNotContain(eventos, e => e.Nombre == "reemplazo");

        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Ruta}/{ConversacionDe(eventos)}");
        var uso = Assert.Single(detalle.GetProperty("herramientas").EnumerateArray());
        Assert.Equal("consultar_servicios_conexos", uso.GetProperty("herramienta").GetString());
        Assert.Equal("ok", uso.GetProperty("resultado").GetString());
        Assert.True(uso.GetProperty("encontrado").GetBoolean());
        Assert.Contains("conexos", uso.GetProperty("fuente").GetString());
        Assert.Equal("""{"top":1}""", uso.GetProperty("entrada").GetString());
        var respuesta = detalle.GetProperty("mensajes")[1];
        Assert.Equal(respuesta.GetProperty("id").GetGuid(), uso.GetProperty("mensajeId").GetGuid());
        Assert.Contains("El costo unitario es $", respuesta.GetProperty("contenido").GetString());
    }

    [Fact]
    public async Task Un_precio_inventado_se_descarta_y_lo_guardado_es_el_aviso_no_la_cifra()
    {
        var yo = NuevoUsuario();
        var c = ClienteDe(Fabrica(PideConexosYCita(citaElDeLaHerramienta: false)), "AdminTarifas", yo);

        var (_, _, eventos) = await Enviar(c, new { mensaje = "¿cuánto cuesta un conexo?" });

        var reemplazo = eventos.Single(e => e.Nombre == "reemplazo").Datos;
        Assert.Equal(["$7.777.777"], reemplazo.GetProperty("cifrasSinOrigen").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("descartado", eventos.Last().Datos.GetProperty("estado").GetString());

        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Ruta}/{ConversacionDe(eventos)}");
        var guardado = detalle.GetProperty("mensajes")[1];
        Assert.Equal("descartado", guardado.GetProperty("estado").GetString());
        Assert.StartsWith("Descarté mi respuesta", guardado.GetProperty("contenido").GetString());
        Assert.DoesNotContain("El costo unitario", guardado.GetProperty("contenido").GetString());
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> PideUnaQueNoPuede(
        Func<clsLlamadaHerramientaIa, CancellationToken, Task<clsResultadoHerramientaIa>> ejecutar, [EnumeratorCancellation] CancellationToken ct)
    {
        var llamada = new clsLlamadaHerramientaIa("toolu_2", "consultar_aprovisionamiento", JsonDocument.Parse("""{"ciudadLevante":"Cartagena","origen":"Medellín"}""").RootElement.Clone());
        yield return new clsEventoConversacionIa.HerramientaSolicitada(llamada);
        var resultado = await ejecutar(llamada, ct);
        yield return new clsEventoConversacionIa.HerramientaTerminada(llamada, resultado);
        yield return new clsEventoConversacionIa.Texto("No tengo permiso para esa consulta.");
        yield return new clsEventoConversacionIa.Fin("end_turn");
    }

    [Fact]
    public async Task Una_herramienta_que_el_rol_no_puede_usar_vuelve_al_modelo_como_error_y_queda_auditada_como_rechazada()
    {
        var yo = NuevoUsuario();
        var c = ClienteDe(Fabrica(Modelo((_, ejecutar, ct) => PideUnaQueNoPuede(ejecutar, ct))), "AdminTarifas", yo);

        var (_, _, eventos) = await Enviar(c, new { mensaje = "calcula el aprovisionamiento" });

        Assert.Equal(["ejecutando", "error"], eventos.Where(e => e.Nombre == "herramienta").Select(e => e.Datos.GetProperty("estado").GetString()));
        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Ruta}/{ConversacionDe(eventos)}");
        var uso = Assert.Single(detalle.GetProperty("herramientas").EnumerateArray());
        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", uso.GetProperty("resultado").GetString());
    }

    // ------------------------------------------------------------ límites e interruptor

    [Fact]
    public async Task Al_llegar_al_limite_de_turnos_responde_429_y_el_modelo_no_se_vuelve_a_llamar()
    {
        var yo = NuevoUsuario();
        var ia = Modelo((_, _, _) => Dice("ok"));
        var fabrica = Fabrica(ia, s =>
        {
            s.RemoveAll<clsLimitesAsistente>();
            s.AddSingleton(new clsLimitesAsistente { MaxTurnos = 2 });
        });
        var c = ClienteDe(fabrica, "AdminTarifas", yo);

        var (_, _, primero) = await Enviar(c, new { mensaje = "uno" });
        var id = ConversacionDe(primero);
        await Enviar(c, new { conversacionId = id, mensaje = "dos" });
        var tercero = await c.PostAsJsonAsync($"{Ruta}/mensajes", new { conversacionId = id, mensaje = "tres" });

        Assert.Equal(HttpStatusCode.TooManyRequests, tercero.StatusCode);
        Assert.Equal("ASISTENTE_LIMITE_TURNOS", await tercero.ErrorCode());
        Assert.Equal(2, ia.Peticiones.Count);
    }

    [Fact]
    public async Task Con_la_funcion_apagada_responde_503_con_el_codigo_y_no_guarda_nada()
    {
        var yo = NuevoUsuario();
        var ia = Modelo((_, _, _) => Dice("ok"));
        ia.ErrorAlVerificar = new clsServiceUnavailableException("IA_FUNCION_DESHABILITADA", "La función de IA está apagada en este ambiente.");
        var c = ClienteDe(Fabrica(ia), "AdminTarifas", yo);

        var resp = await c.PostAsJsonAsync($"{Ruta}/mensajes", new { mensaje = "hola" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
        Assert.Equal("IA_FUNCION_DESHABILITADA", await resp.ErrorCode());
        Assert.Empty((await c.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray());
        Assert.Empty(ia.Peticiones);
    }

    [Fact]
    public async Task El_tope_diario_corta_con_429_y_cada_intercambio_deja_su_fila_de_uso()
    {
        var yo = NuevoUsuario();
        var ia = Modelo((_, _, _) => Dice("ok", entrada: 900, salida: 200));
        var c = ClienteDe(Fabrica(ia, conRegistro: true), "AdminTarifas", yo);

        var (estado, _, _) = await Enviar(c, new { mensaje = "primera" });
        Assert.Equal(HttpStatusCode.OK, estado);

        var uso = fx.WithDb(db => db.UsosIa.Single(u => u.UsuarioId == yo));
        Assert.Equal("AsistenteCotizacion", uso.Funcion);
        Assert.Equal((900, 200), (uso.TokensEntrada, uso.TokensSalida));
        Assert.Equal("ok", uso.Resultado);

        // 1.100 tokens consumidos con un tope de 1.000: la siguiente se corta antes de llamar al modelo.
        var segunda = await c.PostAsJsonAsync($"{Ruta}/mensajes", new { mensaje = "segunda" });

        Assert.Equal(HttpStatusCode.TooManyRequests, segunda.StatusCode);
        Assert.Equal("IA_TOPE_DIARIO", await segunda.ErrorCode());
        Assert.Equal(1, ia.Peticiones.Count);
        Assert.Single((await c.GetFromJsonAsync<JsonElement>(Ruta)).EnumerateArray());
    }

    private static async IAsyncEnumerable<clsEventoConversacionIa> ParteYFalla()
    {
        yield return new clsEventoConversacionIa.Texto("Voy a consultar");
        await Task.Yield();
        throw new clsServiceUnavailableException("IA_NO_DISPONIBLE", "El proveedor de IA no está disponible en este momento.");
    }

    [Fact]
    public async Task Si_el_proveedor_falla_a_mitad_el_flujo_termina_con_un_evento_de_error_y_lo_generado_queda_incompleto()
    {
        var yo = NuevoUsuario();
        var c = ClienteDe(Fabrica(Modelo((_, _, _) => ParteYFalla())), "AdminTarifas", yo);

        var (estado, _, eventos) = await Enviar(c, new { mensaje = "hola" });

        Assert.Equal(HttpStatusCode.OK, estado);
        var error = eventos.Last();
        Assert.Equal("error", error.Nombre);
        Assert.Equal("IA_NO_DISPONIBLE", error.Datos.GetProperty("codigo").GetString());
        var detalle = await c.GetFromJsonAsync<JsonElement>($"{Ruta}/{ConversacionDe(eventos)}");
        var guardado = detalle.GetProperty("mensajes")[1];
        Assert.Equal("incompleto", guardado.GetProperty("estado").GetString());
        Assert.Equal("Voy a consultar", guardado.GetProperty("contenido").GetString());
    }
}
