using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Infrastructure.IA;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Api.Tests;

/// <summary>
/// POST /api/captura-solicitudes/sugerir-mapeo contra la API y la base REALES, con el modelo simulado: quién puede usarlo, que la IA
/// reciba los datos enmascarados, que el diccionario del cliente mande, que la IA apagada o caída no bloquee el flujo manual, el 429 del tope
/// diario y que cada llamada quede registrada en <c>tbl_ia_uso</c>. Ninguna prueba llama a Anthropic.
/// </summary>
[Collection("api")]
public class clsSugerirMapeoApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/captura-solicitudes/sugerir-mapeo";

    private const string MapeoDeLaIa = """
        {"columnas":[
          {"indice":0,"campo":"origen","transformacion":"ninguna","confianza":0.97,"nota":"ciudad de salida"},
          {"indice":1,"campo":"destino","transformacion":"ninguna","confianza":0.97,"nota":""},
          {"indice":2,"campo":"peso","transformacion":"peso_toneladas_a_kg","confianza":0.8,"nota":"viene en toneladas"}
        ],"dudas":["Revisa la unidad del peso."]}
        """;

    private static object Cuerpo(string[]? encabezados = null, string[][]? filas = null, Guid? clienteId = null) => new
    {
        clienteId,
        encabezados = encabezados ?? ["CIUDAD ORIGEN", "CIUDAD DESTINO", "PESO TON", "NIT CLIENTE"],
        filas = filas ?? [["BOGOTA", "CALI", "8", "900123456-1"]],
    };

    private HttpClient ClienteCon(WebApplicationFactory<clsProgram> fabrica, string rol)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    private WebApplicationFactory<clsProgram> FabricaConIa(IAsistenteIaPort ia) =>
        fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAsistenteIaPort>();
            s.AddSingleton(ia);
        }));

    private static string Marca() => $"T359{Guid.NewGuid():N}"[..14];

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Solo_Comercial_y_Admin_piden_una_sugerencia()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.Responde(MapeoDeLaIa));

        foreach (var rol in new[] { "Comercial", "Admin" })
        {
            using var c = ClienteCon(fabrica, rol);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
        }
        foreach (var rol in new[] { "AdminTarifas", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            using var c = ClienteCon(fabrica, rol);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
        }
        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
    }

    // ---------------------------------------------------------------- la propuesta

    [Fact]
    public async Task La_respuesta_trae_una_entrada_por_columna_y_el_modelo_recibe_los_datos_enmascarados()
    {
        var ia = clsAsistenteIaFalso.Responde(MapeoDeLaIa);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.True(dto.GetProperty("iaUsada").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("motivoSinIa").ValueKind);
        var columnas = dto.GetProperty("columnas").EnumerateArray().ToList();
        Assert.Equal(["origen", "destino", "peso", "ignorar"], columnas.Select(x => x.GetProperty("campo").GetString()));
        Assert.Equal("peso_toneladas_a_kg", columnas[2].GetProperty("transformacion").GetString());
        Assert.Equal("ia", columnas[0].GetProperty("origen").GetString());
        Assert.Equal("ninguno", columnas[3].GetProperty("origen").GetString());
        Assert.Contains(dto.GetProperty("dudas").EnumerateArray().Select(x => x.GetString()), d => d == "Revisa la unidad del peso.");

        var enviada = Assert.Single(ia.Peticiones);
        Assert.Equal(clsNexoDbContext.UsuarioSistemaId, enviada.UsuarioId);
        Assert.DoesNotContain("900123456", enviada.Mensajes.Single().Contenido);
        Assert.Contains("<NIT>", enviada.Mensajes.Single().Contenido);
    }

    [Fact]
    public async Task El_diccionario_del_cliente_manda_y_si_resuelve_todo_no_se_llama_a_la_IA()
    {
        var marca = Marca();
        var cliente = fx.WithDb(db => db.Clientes.First().Id);
        var ia = clsAsistenteIaFalso.Responde(MapeoDeLaIa);
        using var fabrica = FabricaConIa(ia);
        using var admin = ClienteCon(fabrica, "Admin");
        try
        {
            var termino = await admin.PostAsJsonAsync("/api/diccionario-cliente",
                new { clienteId = cliente, termino = $"{marca} SALE DE", tipo = "columna", concepto = "origen" });
            Assert.Equal(HttpStatusCode.Created, termino.StatusCode);

            var resp = await admin.PostAsJsonAsync(Ruta, Cuerpo(encabezados: [$"{marca} sale de"], filas: [["BOGOTA"]], clienteId: cliente));

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var dto = await resp.Json();
            Assert.False(dto.GetProperty("iaUsada").GetBoolean());
            Assert.Equal("DICCIONARIO_COMPLETO", dto.GetProperty("motivoSinIa").GetString());
            var columna = Assert.Single(dto.GetProperty("columnas").EnumerateArray());
            Assert.Equal(("origen", "diccionario", 1.0), (columna.GetProperty("campo").GetString(), columna.GetProperty("origen").GetString(), columna.GetProperty("confianza").GetDouble()));
            Assert.Empty(ia.Peticiones);
        }
        finally { fx.WithDb(db => db.TerminosCliente.Where(t => t.Termino.StartsWith(marca)).ExecuteDelete()); }
    }

    // ---------------------------------------------------------------- la IA no bloquea el flujo manual

    [Fact]
    public async Task Con_la_IA_apagada_responde_200_sin_sugerencia_y_con_el_motivo()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.False(dto.GetProperty("iaUsada").GetBoolean());
        Assert.Equal("IA_NO_DISPONIBLE", dto.GetProperty("motivoSinIa").GetString());
        Assert.Equal(4, dto.GetProperty("columnas").GetArrayLength());
        Assert.All(dto.GetProperty("columnas").EnumerateArray(), x => Assert.Equal("ignorar", x.GetProperty("campo").GetString()));
    }

    [Fact]
    public async Task Si_el_proveedor_falla_responde_200_con_el_codigo_del_error()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.Falla("IA_LIMITE_PROVEEDOR"));
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("IA_LIMITE_PROVEEDOR", (await resp.Json()).GetProperty("motivoSinIa").GetString());
    }

    [Fact]
    public async Task Un_tope_diario_superado_responde_429_con_su_codigo()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.Lanza(
            new clsLimiteExcedidoException("IA_TOPE_DIARIO", "Alcanzaste el tope.", new { limiteTokensPorDia = 500 })));
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        Assert.Equal("IA_TOPE_DIARIO", await resp.ErrorCode());
    }

    // ---------------------------------------------------------------- entrada

    [Fact]
    public async Task Un_cuerpo_sin_encabezados_o_con_demasiados_es_400()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.Responde(MapeoDeLaIa));
        using var c = ClienteCon(fabrica, "Comercial");

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { filas = new[] { new[] { "x" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { encabezados = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync(Ruta, new { encabezados = Enumerable.Range(0, 101).Select(i => $"C{i}").ToArray() })).StatusCode);
    }

    // ---------------------------------------------------------------- de punta a punta con el registro de uso

    private sealed class clsProveedorSimulado(string modelo, string textoDelModelo) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Llamadas++;
            var json = JsonSerializer.Serialize(new
            {
                id = "msg_prueba", type = "message", role = "assistant", model = modelo,
                content = new[] { new { type = "text", text = textoDelModelo } },
                stop_reason = "end_turn", stop_sequence = (string?)null,
                usage = new { input_tokens = 640, output_tokens = 210 },
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task La_llamada_real_pasa_por_el_decorador_y_queda_registrada_en_tbl_ia_uso_a_nombre_de_quien_la_pidio()
    {
        var modelo = $"prueba-mapeo-{Guid.NewGuid():N}";
        var proveedor = new clsProveedorSimulado(modelo, MapeoDeLaIa);
        using var fabrica = fx.Factory.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "sk-ant-llave-falsa-de-prueba",
                ["Anthropic:Model"] = modelo,
                ["Anthropic:Funciones:ImportacionExcel"] = "true",
                ["Anthropic:LimiteTokensPorUsuarioDia"] = "0",
            }));
            b.ConfigureTestServices(s =>
            {
                s.RemoveAll<clsAnthropicAdapter>();
                s.AddSingleton(sp => new clsAnthropicAdapter(
                    sp.GetRequiredService<IOptions<clsOpcionesAnthropic>>(), NullLogger<clsAnthropicAdapter>.Instance,
                    new HttpClient(proveedor), "http://proveedor.prueba"));
            });
        });
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var resp = await c.PostAsJsonAsync(Ruta, Cuerpo());

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.True((await resp.Json()).GetProperty("iaUsada").GetBoolean());
            Assert.Equal(1, proveedor.Llamadas);

            var fila = Assert.Single(fx.WithDb(db => db.UsosIa.Where(u => u.Modelo == modelo).ToList()));
            Assert.Equal(("ImportacionExcel", clsNexoDbContext.UsuarioSistemaId, "ok", 640, 210),
                (fila.Funcion, fila.UsuarioId, fila.Resultado, fila.TokensEntrada, fila.TokensSalida));
        }
        finally { fx.WithDb(db => db.UsosIa.Where(u => u.Modelo == modelo).ExecuteDelete()); }
    }
}
