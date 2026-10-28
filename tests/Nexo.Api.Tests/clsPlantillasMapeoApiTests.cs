using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Ports;

namespace Nexo.Api.Tests;

/// <summary>
/// Las plantillas de mapeo contra la API y la base REALES: guardar (201) y reemplazar (200), listar, borrar, los roles, y que
/// <c>sugerir-mapeo</c> use la plantilla sin llamar a la IA mientras los encabezados no cambien y vuelva a sugerir si cambian.
/// </summary>
[Collection("api")]
public class clsPlantillasMapeoApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/captura-solicitudes/plantillas-mapeo";
    private const string RutaSugerir = "/api/captura-solicitudes/sugerir-mapeo";

    private const string RespuestaDeLaIa = """{"columnas":[{"indice":0,"campo":"origen","transformacion":"ninguna","confianza":0.9,"nota":""}],"dudas":[]}""";

    private static object Columnas() => new object[]
    {
        new { indice = 0, campo = "origen" }, new { indice = 1, campo = "destino" },
        new { indice = 2, campo = "peso", transformacion = "peso_toneladas_a_kg" }, new { indice = 3, campo = "ignorar" },
    };

    private static object Cuerpo(Guid? cliente, string[] encabezados, string? nombre = null, object? columnas = null) =>
        new { clienteId = cliente, nombre, encabezados, columnas = columnas ?? Columnas() };

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

    private static string[] EncabezadosUnicos(string marca) => [$"{marca} Ciudad origen", $"{marca} Ciudad destino", $"{marca} Peso (TON)", $"{marca} NIT"];

    private static string Marca() => $"T365{Guid.NewGuid():N}"[..14];

    private Guid Cliente() => fx.WithDb(db => db.Clientes.First().Id);

    private void Limpiar(Guid cliente, string marca) =>
        fx.WithDb(db => db.PlantillasMapeo.Where(p => p.ClienteId == cliente && p.Encabezados.Contains(marca)).ExecuteDelete());

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Solo_Comercial_y_Admin_usan_las_plantillas()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        try
        {
            foreach (var rol in new[] { "Comercial", "Admin" })
            {
                using var c = ClienteCon(fabrica, rol);
                Assert.True((await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))).IsSuccessStatusCode);
                Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Ruta}?clienteId={cliente}")).StatusCode);
            }
            foreach (var rol in new[] { "AdminTarifas", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
            {
                using var c = ClienteCon(fabrica, rol);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Ruta)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync($"{Ruta}/{Guid.NewGuid()}")).StatusCode);
            }
            using var anonimo = fabrica.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))).StatusCode);
        }
        finally { Limpiar(cliente, marca); }
    }

    // ---------------------------------------------------------------- guardar, listar y borrar

    [Fact]
    public async Task Guardar_crea_con_201_y_volver_a_guardar_los_mismos_encabezados_reemplaza_con_200()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var primera = await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca), nombre: "BASF"));
            Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
            var dto = await primera.Json();
            Assert.Equal(("BASF", 4, 4), (dto.GetProperty("nombre").GetString(), dto.GetProperty("encabezados").GetArrayLength(), dto.GetProperty("columnas").GetArrayLength()));
            var id = dto.GetProperty("id").GetGuid();

            // Los mismos encabezados con otra mayúscula: es la misma plantilla.
            var mayusculas = EncabezadosUnicos(marca).Select(e => e.ToUpperInvariant()).ToArray();
            var segunda = await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, mayusculas, nombre: "BASF v2", columnas: new object[] { new { indice = 0, campo = "origen" }, new { indice = 1, campo = "destino" } }));
            Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
            var reemplazada = await segunda.Json();
            Assert.Equal((id, "BASF v2", 2), (reemplazada.GetProperty("id").GetGuid(), reemplazada.GetProperty("nombre").GetString(), reemplazada.GetProperty("columnas").GetArrayLength()));
            Assert.Equal(1, fx.WithDb(db => db.PlantillasMapeo.Count(p => p.ClienteId == cliente && p.Encabezados.Contains(marca))));
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Listar_trae_las_del_cliente_y_borrar_la_quita()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var id = (await (await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))).Json()).GetProperty("id").GetGuid();

            var lista = await (await c.GetAsync($"{Ruta}?clienteId={cliente}")).Json();
            Assert.Contains(lista.EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);
            Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Ruta}/{id}")).StatusCode);
            Assert.DoesNotContain((await (await c.GetAsync($"{Ruta}?clienteId={cliente}")).Json()).EnumerateArray(), p => p.GetProperty("id").GetGuid() == id);

            var otraVez = await c.DeleteAsync($"{Ruta}/{id}");
            Assert.Equal(HttpStatusCode.NotFound, otraVez.StatusCode);
            Assert.Equal("PLANTILLA_NO_ENCONTRADA", await otraVez.ErrorCode());
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Un_cuerpo_o_un_mapeo_invalido_es_400_con_su_codigo()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        async Task Esperar(object cuerpo, string? codigo)
        {
            var resp = await c.PutAsJsonAsync(Ruta, cuerpo);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            if (codigo is not null) Assert.Equal(codigo, await resp.ErrorCode());
        }

        await Esperar(new { encabezados = EncabezadosUnicos(marca), columnas = Columnas() }, null);                                  // sin cliente
        await Esperar(Cuerpo(Guid.NewGuid(), EncabezadosUnicos(marca)), "CLIENTE_NO_ENCONTRADO");
        await Esperar(Cuerpo(cliente, [], null), null);
        await Esperar(Cuerpo(cliente, EncabezadosUnicos(marca), columnas: new object[] { new { indice = 0, campo = "origen" }, new { indice = 1, campo = "inventado" } }), "CAMPO_INVALIDO");
        await Esperar(Cuerpo(cliente, EncabezadosUnicos(marca), columnas: new object[] { new { indice = 0, campo = "origen" } }), "CAMPO_REQUERIDO");
        await Esperar(Cuerpo(cliente, EncabezadosUnicos(marca), columnas: new object[] { new { indice = 0, campo = "origen" }, new { indice = 1, campo = "destino" }, new { indice = 9, campo = "peso" } }), "COLUMNA_FUERA_DE_RANGO");
        Assert.Equal(0, fx.WithDb(db => db.PlantillasMapeo.Count(p => p.Encabezados.Contains(marca))));
    }

    // ---------------------------------------------------------------- reusar en sugerir-mapeo

    [Fact]
    public async Task Con_los_mismos_encabezados_sugerir_mapeo_usa_la_plantilla_sin_llamar_a_la_IA_y_si_cambian_vuelve_a_sugerir()
    {
        var cliente = Cliente();
        var marca = Marca();
        var ia = clsAsistenteIaFalso.Responde(RespuestaDeLaIa);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var plantilla = (await (await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))).Json()).GetProperty("id").GetGuid();
            var fila = new[] { new[] { "BOGOTA", "CALI", "8", "900123456-1" } };

            // Igual (con otras mayúsculas): la plantilla manda y la IA no se toca.
            var igual = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteId = cliente, encabezados = EncabezadosUnicos(marca).Select(e => e.ToLowerInvariant()), filas = fila })).Json();
            Assert.False(igual.GetProperty("iaUsada").GetBoolean());
            Assert.Equal("PLANTILLA_RECONOCIDA", igual.GetProperty("motivoSinIa").GetString());
            Assert.Equal(plantilla, igual.GetProperty("plantillaId").GetGuid());
            var columnas = igual.GetProperty("columnas").EnumerateArray().ToList();
            Assert.Equal(["origen", "destino", "peso", "ignorar"], columnas.Select(x => x.GetProperty("campo").GetString()));
            Assert.Equal("peso_toneladas_a_kg", columnas[2].GetProperty("transformacion").GetString());
            Assert.All(columnas, x => Assert.Equal("plantilla", x.GetProperty("origen").GetString()));
            Assert.Empty(ia.Peticiones);

            // Otro cliente o sin cliente: no hay plantilla, se sugiere con la IA.
            var sinCliente = await (await c.PostAsJsonAsync(RutaSugerir, new { encabezados = EncabezadosUnicos(marca), filas = fila })).Json();
            Assert.True(sinCliente.GetProperty("iaUsada").GetBoolean());
            Assert.Equal(JsonKindNull, sinCliente.GetProperty("plantillaId").ValueKind);

            // Cambia un encabezado: se vuelve a sugerir.
            var cambiados = EncabezadosUnicos(marca); cambiados[2] = $"{marca} Peso (KG)";
            var otra = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteId = cliente, encabezados = cambiados, filas = fila })).Json();
            Assert.True(otra.GetProperty("iaUsada").GetBoolean());
            Assert.Equal(JsonKindNull, otra.GetProperty("plantillaId").ValueKind);
            Assert.Equal(2, ia.Peticiones.Count);

            // Borrada la plantilla, los encabezados originales también se vuelven a sugerir.
            Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Ruta}/{plantilla}")).StatusCode);
            var tras = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteId = cliente, encabezados = EncabezadosUnicos(marca), filas = fila })).Json();
            Assert.True(tras.GetProperty("iaUsada").GetBoolean());
        }
        finally { Limpiar(cliente, marca); }
    }

    // ---------------------------------------------------------------- guardar sin duplicar, ni romper

    [Fact]
    public async Task Varias_confirmaciones_simultaneas_del_mismo_formato_dejan_una_sola_plantilla_y_ninguna_falla()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            // Un doble clic en «Confirmar»: todas buscan, ninguna la encuentra, todas intentan insertar; el índice único deja pasar una.
            var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)))));

            Assert.All(respuestas, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"respondió {(int)r.StatusCode}"));
            Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, fx.WithDb(db => db.PlantillasMapeo.Count(p => p.ClienteId == cliente && p.Encabezados.Contains(marca))));
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Las_columnas_se_guardan_con_su_version_y_los_encabezados_con_columnas_vacias_de_mas_son_la_misma_plantilla()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var primera = await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)));
            Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
            Assert.StartsWith("""{"version":1,"columnas":[""", fx.WithDb(db => db.PlantillasMapeo.Single(p => p.ClienteId == cliente && p.Encabezados.Contains(marca)).Columnas));

            // Excel reporta tres columnas vacías de más al final: es el mismo formato.
            var conVacias = await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, [.. EncabezadosUnicos(marca), "", " ", ""]));
            Assert.Equal(HttpStatusCode.OK, conVacias.StatusCode);
            Assert.Equal(1, fx.WithDb(db => db.PlantillasMapeo.Count(p => p.ClienteId == cliente && p.Encabezados.Contains(marca))));
        }
        finally { Limpiar(cliente, marca); }
    }

    // ---------------------------------------------------------------- una plantilla vieja no se usa

    [Theory]
    [InlineData("""{"version":2,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"}]}""")]    // una forma que este código no conoce
    [InlineData("""{"version":1,"columnas":[{"indice":0,"campo":"origen"},{"indice":1,"campo":"campo_retirado"}]}""")] // un campo que ya no existe
    [InlineData("""[{"indice":0,"campo":"origen"},{"indice":1,"campo":"destino"}]""")]                             // sin versión
    public async Task Una_plantilla_guardada_que_ya_no_sirve_se_ignora_y_se_vuelve_a_sugerir_con_aviso(string columnasGuardadas)
    {
        var cliente = Cliente();
        var marca = Marca();
        var ia = clsAsistenteIaFalso.Responde(RespuestaDeLaIa);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)));
            fx.WithDb(db => db.PlantillasMapeo.Where(p => p.ClienteId == cliente && p.Encabezados.Contains(marca)).ExecuteUpdate(s => s.SetProperty(p => p.Columnas, columnasGuardadas)));

            var dto = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteId = cliente, encabezados = EncabezadosUnicos(marca), filas = new[] { new[] { "BOGOTA", "CALI", "8", "x" } } })).Json();

            Assert.True(dto.GetProperty("iaUsada").GetBoolean());
            Assert.Equal(JsonKindNull, dto.GetProperty("plantillaId").ValueKind);
            Assert.Contains(dto.GetProperty("dudas").EnumerateArray(), d => d.GetString()!.Contains("ya no sirve"));
            Assert.Single(ia.Peticiones);
        }
        finally { Limpiar(cliente, marca); }
    }

    // ---------------------------------------------------------------- que haya plantilla no salta las validaciones

    [Fact]
    public async Task Con_plantilla_las_validaciones_del_mapeo_siguen_corriendo_y_reportan_el_error_de_cada_fila()
    {
        var cliente = Cliente();
        var marca = Marca();
        var ia = clsAsistenteIaFalso.Responde(RespuestaDeLaIa);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            await c.PutAsJsonAsync(Ruta, Cuerpo(cliente, EncabezadosUnicos(marca)));
            var filas = new[]
            {
                new[] { "BOGOTA", "CALI", "8", "x" },          // fila 2: válida (8 t = 8.000 kg)
                new[] { "BOGOTA", "CALI", "no es un peso", "x" }, // fila 3: peso ilegible
                new[] { "BOGOTA", "CALI", "-5", "x" },         // fila 4: peso negativo
            };

            var sugerida = await (await c.PostAsJsonAsync(RutaSugerir, new { clienteId = cliente, encabezados = EncabezadosUnicos(marca), filas })).Json();
            Assert.Equal("PLANTILLA_RECONOCIDA", sugerida.GetProperty("motivoSinIa").GetString());
            var columnas = sugerida.GetProperty("columnas").EnumerateArray()
                .Select(x => new { indice = x.GetProperty("indice").GetInt32(), campo = x.GetProperty("campo").GetString(), transformacion = x.GetProperty("transformacion").GetString() }).ToArray();

            var aplicado = await (await c.PostAsJsonAsync("/api/captura-solicitudes/aplicar-mapeo", new { clienteId = cliente, columnas, filas, primeraFila = 2 })).Json();

            Assert.Equal(3, aplicado.GetProperty("totalFilas").GetInt32());
            Assert.Equal(2, aplicado.GetProperty("filasConErrores").GetInt32());
            Assert.Equal([2], aplicado.GetProperty("lineas").EnumerateArray().Select(l => l.GetProperty("fila").GetInt32()));
            Assert.Equal(8000m, aplicado.GetProperty("lineas")[0].GetProperty("peso").GetDecimal());
            Assert.Equal([3, 4], aplicado.GetProperty("errores").EnumerateArray().Select(e => e.GetProperty("fila").GetInt32()).Distinct().Order());
        }
        finally { Limpiar(cliente, marca); }
    }

    private const System.Text.Json.JsonValueKind JsonKindNull = System.Text.Json.JsonValueKind.Null;
}
