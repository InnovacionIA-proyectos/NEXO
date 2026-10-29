using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Api.Tests;

/// <summary>
/// POST /api/captura-solicitudes/normalizar-valores contra la API y la base REALES, con el modelo simulado: quién puede usarlo, que las reglas
/// resuelvan con el catálogo real de municipios, que el diccionario del cliente mande, que la IA reciba una sola llamada y que lo que elige
/// solo se acepte si está en la lista dada. Ninguna prueba llama a Anthropic.
/// </summary>
[Collection("api")]
public class clsNormalizarValoresApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/captura-solicitudes/normalizar-valores";

    private static object Cuerpo(object[] valores, Guid? clienteId = null) => new { clienteId, valores };

    private static object V(string campo, string valor) => new { campo, valor };

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

    private static string Marca() => $"T362{Guid.NewGuid():N}"[..14];

    private int MunicipioId(string codigoDane) => fx.WithDb(db => db.Municipios.First(m => m.CodigoDane == codigoDane && m.Pais == "CO").Id);

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Solo_Comercial_y_Admin_normalizan_valores()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        var cuerpo = Cuerpo([V("ciudad", "76001")]);

        foreach (var rol in new[] { "Comercial", "Admin" })
        {
            using var c = ClienteCon(fabrica, rol);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(Ruta, cuerpo)).StatusCode);
        }
        foreach (var rol in new[] { "AdminTarifas", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            using var c = ClienteCon(fabrica, rol);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Ruta, cuerpo)).StatusCode);
        }
        using var anonimo = fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(Ruta, cuerpo)).StatusCode);
    }

    // ---------------------------------------------------------------- reglas exactas sobre el catálogo real

    [Fact]
    public async Task Un_codigo_DANE_y_un_nombre_se_resuelven_con_los_municipios_reales_sin_llamar_a_la_IA()
    {
        var ia = clsAsistenteIaFalso.Responde("""{"resultados":[]}""");
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", "76001"), V("ciudad", "05001000"), V("ciudad", "medellin")]));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.False(dto.GetProperty("iaUsada").GetBoolean());
        Assert.Equal("SIN_PENDIENTES", dto.GetProperty("motivoSinIa").GetString());
        var valores = dto.GetProperty("valores").EnumerateArray().ToList();
        Assert.All(valores, v => Assert.Equal("resuelto", v.GetProperty("estado").GetString()));
        Assert.Equal(["dane", "dane", "catalogo"], valores.Select(v => v.GetProperty("origen").GetString()));
        Assert.Equal([MunicipioId("76001000"), MunicipioId("05001000"), MunicipioId("05001000")], valores.Select(v => v.GetProperty("municipioId").GetInt32()));
        Assert.Empty(ia.Peticiones);
    }

    [Fact]
    public async Task El_diccionario_del_cliente_traduce_el_vehiculo_y_no_se_filtra_a_otros()
    {
        var marca = Marca();
        var cliente = fx.WithDb(db => db.Clientes.First().Id);
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var admin = ClienteCon(fabrica, "Admin");
        try
        {
            var creado = await admin.PostAsJsonAsync("/api/diccionario-cliente",
                new { clienteId = cliente, termino = $"{marca} MULA", tipo = "valor", concepto = "3S3", contexto = "tipoVehiculo" });
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
            var valores = new[] { V("tipoVehiculo", $"{marca} mula") };

            var delCliente = (await (await admin.PostAsJsonAsync(Ruta, Cuerpo(valores, cliente))).Json()).GetProperty("valores")[0];
            var sinCliente = (await (await admin.PostAsJsonAsync(Ruta, Cuerpo(valores))).Json()).GetProperty("valores")[0];

            Assert.Equal(("resuelto", "3S3", "diccionario"), (delCliente.GetProperty("estado").GetString(), delCliente.GetProperty("codigo").GetString(), delCliente.GetProperty("origen").GetString()));
            Assert.Equal("sin_resolver", sinCliente.GetProperty("estado").GetString());
        }
        finally { fx.WithDb(db => db.TerminosCliente.Where(t => t.Termino.StartsWith(marca)).ExecuteDelete()); }
    }

    // ---------------------------------------------------------------- la IA

    [Fact]
    public async Task Una_ciudad_mal_escrita_va_a_la_IA_una_sola_vez_y_solo_se_acepta_un_municipio_candidato()
    {
        var medellin = MunicipioId("05001000");
        var ia = clsAsistenteIaFalso.Responde(JsonSerializer.Serialize(new
        {
            resultados = new[] { new { indice = 0, clave = medellin.ToString(), confianza = 0.9, nota = "falta una l" }, new { indice = 1, clave = "999999", confianza = 0.95, nota = "" } },
        }));
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", "Medelin"), V("ciudad", "Medellin Antioquia Colombia Zona Norte")]));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.True(dto.GetProperty("iaUsada").GetBoolean());
        var primero = dto.GetProperty("valores")[0];
        Assert.Equal(("resuelto", "ia", medellin), (primero.GetProperty("estado").GetString(), primero.GetProperty("origen").GetString(), primero.GetProperty("municipioId").GetInt32()));
        Assert.NotEqual("resuelto", dto.GetProperty("valores")[1].GetProperty("estado").GetString()); // una clave fuera de los candidatos no se acepta

        var enviada = Assert.Single(ia.Peticiones);
        Assert.Equal(Nexo.Infrastructure.Persistence.clsNexoDbContext.UsuarioSistemaId, enviada.UsuarioId);
    }

    [Fact]
    public async Task Con_la_IA_apagada_o_caida_responde_200_con_lo_de_las_reglas_y_el_motivo()
    {
        using (var apagada = FabricaConIa(clsAsistenteIaFalso.SinConfigurar()))
        using (var c = ClienteCon(apagada, "Comercial"))
        {
            var dto = await (await c.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", "76001"), V("ciudad", "Medelin")]))).Json();
            Assert.False(dto.GetProperty("iaUsada").GetBoolean());
            Assert.Equal("IA_NO_DISPONIBLE", dto.GetProperty("motivoSinIa").GetString());
            Assert.Equal(["resuelto", "sin_resolver"], dto.GetProperty("valores").EnumerateArray().Select(v => v.GetProperty("estado").GetString()));
        }

        using var caida = FabricaConIa(clsAsistenteIaFalso.Falla("IA_LIMITE_PROVEEDOR"));
        using var c2 = ClienteCon(caida, "Comercial");
        var dto2 = await (await c2.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", "Medelin")]))).Json();
        Assert.Equal("IA_LIMITE_PROVEEDOR", dto2.GetProperty("motivoSinIa").GetString());
    }

    [Fact]
    public async Task Un_tope_diario_superado_responde_429()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.Lanza(new clsLimiteExcedidoException("IA_TOPE_DIARIO", "Alcanzaste el tope.", new { limiteTokensPorDia = 500 })));
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", "Medelin")]));

        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        Assert.Equal("IA_TOPE_DIARIO", await resp.ErrorCode());
    }

    // ---------------------------------------------------------------- línea de negocio

    [Fact]
    public async Task La_linea_de_negocio_se_resuelve_con_el_diccionario_del_cliente_los_sinonimos_y_la_IA_en_ese_orden()
    {
        var marca = Marca();
        var cliente = fx.WithDb(db => db.Clientes.First().Id);
        var ia = clsAsistenteIaFalso.Responde("""{"resultados":[{"indice":0,"clave":"devolucionContenedor","confianza":0.9,"nota":""}]}""");
        using var fabrica = FabricaConIa(ia);
        using var admin = ClienteCon(fabrica, "Admin");
        try
        {
            var creado = await admin.PostAsJsonAsync("/api/diccionario-cliente",
                new { clienteId = cliente, termino = $"{marca} Outbound", tipo = "valor", concepto = "Interior a puerto", contexto = "lineaNegocio" });
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

            var resp = await admin.PostAsJsonAsync(Ruta, Cuerpo(
                [V("lineaNegocio", $"{marca} outbound"), V("lineaNegocio", "IMPO"), V("lineaNegocio", $"{marca} vacios")], cliente));

            var valores = (await resp.Json()).GetProperty("valores").EnumerateArray().ToList();
            Assert.Equal(
                [("resuelto", "interiorAPuerto", "diccionario"), ("resuelto", "puertoAInterior", "regla"), ("resuelto", "devolucionContenedor", "ia")],
                valores.Select(v => (v.GetProperty("estado").GetString(), v.GetProperty("codigo").GetString(), v.GetProperty("origen").GetString())));
            Assert.Single(ia.Peticiones);
        }
        finally { fx.WithDb(db => db.TerminosCliente.Where(t => t.Termino.StartsWith(marca)).ExecuteDelete()); }
    }

    // ---------------------------------------------------------------- entrada

    [Fact]
    public async Task Una_entrada_invalida_es_400()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { clienteId = (Guid?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, Cuerpo([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, Cuerpo([V("ciudad", new string('x', 151))]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, Cuerpo(Enumerable.Range(0, 1001).Select(i => V("ciudad", $"C{i}")).ToArray()))).StatusCode);

        var campo = await c.PostAsJsonAsync(Ruta, Cuerpo([V("peso", "8")]));
        Assert.Equal(HttpStatusCode.BadRequest, campo.StatusCode);
        Assert.Equal("CAMPO_INVALIDO", await campo.ErrorCode());
    }
}
