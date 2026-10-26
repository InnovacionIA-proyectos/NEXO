using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// El diccionario de términos por cliente contra la API y la base REALES (<c>tbl_diccionario_cliente</c>): quién lee y quién escribe,
/// el ciclo completo crear, listar, editar y borrar, los duplicados y las validaciones. Todo lo que crea lleva una marca única en el
/// término y se borra al terminar.
/// </summary>
[Collection("api")]
public class clsDiccionarioClienteApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/diccionario-cliente";

    private static string Marca() => $"T358{Guid.NewGuid():N}"[..14];

    private Guid ClienteExistente() => fx.WithDb(db => db.Clientes.First().Id);

    private void Limpiar(string marca) =>
        fx.WithDb(db => db.TerminosCliente.Where(t => t.Termino.StartsWith(marca)).ExecuteDelete());

    private static object Termino(string termino, string tipo = "columna", string concepto = "origen", Guid? clienteId = null,
        string? contexto = null, string? ejemplo = null, bool activo = true) =>
        new { clienteId, termino, tipo, concepto, contexto, ejemplo, activo };

    private static async Task<JsonElement> Cuerpo(HttpResponseMessage r) => await r.Json();

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Leer_el_diccionario_lo_puede_cualquier_rol_valido()
    {
        foreach (var rol in new[] { "Comercial", "AdminBiblioteca", "AdminTarifas", "ServiciosLogisticos", "LiderProceso", "Admin" })
        {
            using var c = fx.Client(rol);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Ruta)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Ruta}/opciones")).StatusCode);
        }
    }

    [Fact]
    public async Task Sin_identidad_no_se_lee_el_diccionario()
    {
        using var anonimo = fx.Anon();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(Ruta)).StatusCode);
    }

    [Fact]
    public async Task Escribir_solo_lo_pueden_AdminBiblioteca_AdminTarifas_y_Admin()
    {
        var marca = Marca();
        try
        {
            foreach (var rol in new[] { "Comercial", "ServiciosLogisticos", "LiderProceso" })
            {
                using var c = fx.Client(rol);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Ruta, Termino($"{marca} {rol}"))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"{Ruta}/{Guid.NewGuid()}", Termino("x"))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync($"{Ruta}/{Guid.NewGuid()}")).StatusCode);
            }
            foreach (var rol in new[] { "AdminBiblioteca", "AdminTarifas", "Admin" })
            {
                using var c = fx.Client(rol);
                Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Ruta, Termino($"{marca} {rol}"))).StatusCode);
            }
        }
        finally { Limpiar(marca); }
    }

    // ---------------------------------------------------------------- ciclo completo

    [Fact]
    public async Task Crear_listar_editar_y_borrar_un_termino_de_un_cliente()
    {
        var marca = Marca();
        var cliente = ClienteExistente();
        using var admin = fx.Client("AdminTarifas");
        try
        {
            var creado = await admin.PostAsJsonAsync(Ruta, Termino($"{marca} CIUDAD ORIGEN", ejemplo: "BOGOTA", clienteId: cliente));
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
            var dto = await Cuerpo(creado);
            var id = dto.GetProperty("id").GetGuid();
            Assert.Equal(cliente, dto.GetProperty("clienteId").GetGuid());
            Assert.False(string.IsNullOrWhiteSpace(dto.GetProperty("clienteNombre").GetString()));
            Assert.Equal(("columna", "origen", true), (dto.GetProperty("tipo").GetString(), dto.GetProperty("concepto").GetString(), dto.GetProperty("activo").GetBoolean()));

            var lista = await Cuerpo(await admin.GetAsync($"{Ruta}?clienteId={cliente}&q={marca}"));
            Assert.Equal(id, Assert.Single(lista.EnumerateArray()).GetProperty("id").GetGuid());

            var editado = await admin.PutAsJsonAsync($"{Ruta}/{id}", Termino($"{marca} CIUDAD DE ORIGEN", concepto: "ORIGEN", activo: false, ejemplo: "MEDELLIN"));
            Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
            var dtoEditado = await Cuerpo(editado);
            Assert.Equal($"{marca} CIUDAD DE ORIGEN", dtoEditado.GetProperty("termino").GetString());
            Assert.False(dtoEditado.GetProperty("activo").GetBoolean());
            Assert.Equal(cliente, dtoEditado.GetProperty("clienteId").GetGuid());
            Assert.NotEqual(JsonValueKind.Null, dtoEditado.GetProperty("updatedAt").ValueKind);

            var soloActivos = await Cuerpo(await admin.GetAsync($"{Ruta}?clienteId={cliente}&q={marca}&soloActivos=true"));
            Assert.Empty(soloActivos.EnumerateArray());

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{Ruta}/{id}")).StatusCode);
            var otra = await admin.DeleteAsync($"{Ruta}/{id}");
            Assert.Equal(HttpStatusCode.NotFound, otra.StatusCode);
            Assert.Equal("TERMINO_NO_ENCONTRADO", await otra.ErrorCode());
            Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"{Ruta}/{id}", Termino("x"))).StatusCode);
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Los_terminos_generales_aparecen_con_el_cliente_y_se_pueden_omitir()
    {
        var marca = Marca();
        var cliente = ClienteExistente();
        using var admin = fx.Client("Admin");
        try
        {
            await admin.PostAsJsonAsync(Ruta, Termino($"{marca} general", tipo: "unidad", concepto: "kg"));
            await admin.PostAsJsonAsync(Ruta, Termino($"{marca} propio", tipo: "valor", concepto: "2_7", contexto: "tipoVehiculo", clienteId: cliente));

            var con = await Cuerpo(await admin.GetAsync($"{Ruta}?clienteId={cliente}&q={marca}"));
            Assert.Equal(2, con.GetArrayLength());
            // Primero los generales (sin cliente) y luego los del cliente.
            Assert.Equal(JsonValueKind.Null, con[0].GetProperty("clienteId").ValueKind);

            var sin = await Cuerpo(await admin.GetAsync($"{Ruta}?clienteId={cliente}&incluirGenerales=false&q={marca}"));
            Assert.Equal($"{marca} propio", Assert.Single(sin.EnumerateArray()).GetProperty("termino").GetString());

            var porTipo = await Cuerpo(await admin.GetAsync($"{Ruta}?tipo=unidad&q={marca}"));
            Assert.Equal($"{marca} general", Assert.Single(porTipo.EnumerateArray()).GetProperty("termino").GetString());
        }
        finally { Limpiar(marca); }
    }

    // ---------------------------------------------------------------- duplicados

    [Fact]
    public async Task Un_termino_repetido_es_409_aunque_cambien_mayusculas_tildes_o_espacios()
    {
        var marca = Marca();
        var cliente = ClienteExistente();
        using var admin = fx.Client("AdminBiblioteca");
        try
        {
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Ruta, Termino($"{marca} Ciudad Origen", clienteId: cliente))).StatusCode);

            var repetido = await admin.PostAsJsonAsync(Ruta, Termino($"{marca.ToLowerInvariant()}   ciudad  origén", concepto: "destino", clienteId: cliente));
            Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
            Assert.Equal("TERMINO_DUPLICADO", await repetido.ErrorCode());

            // Otro tipo, otro cliente (o general) con el mismo texto sí se acepta.
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Ruta, Termino($"{marca} Ciudad Origen", tipo: "valor", concepto: "x", contexto: "carroceria", clienteId: cliente))).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync(Ruta, Termino($"{marca} Ciudad Origen"))).StatusCode);
        }
        finally { Limpiar(marca); }
    }

    [Fact]
    public async Task Editar_un_termino_para_que_coincida_con_otro_del_mismo_cliente_es_409()
    {
        var marca = Marca();
        var cliente = ClienteExistente();
        using var admin = fx.Client("Admin");
        try
        {
            await admin.PostAsJsonAsync(Ruta, Termino($"{marca} uno", clienteId: cliente));
            var segundo = await Cuerpo(await admin.PostAsJsonAsync(Ruta, Termino($"{marca} dos", clienteId: cliente)));

            var choque = await admin.PutAsJsonAsync($"{Ruta}/{segundo.GetProperty("id").GetGuid()}", Termino($"{marca} UNO"));

            Assert.Equal(HttpStatusCode.Conflict, choque.StatusCode);
            Assert.Equal("TERMINO_DUPLICADO", await choque.ErrorCode());
        }
        finally { Limpiar(marca); }
    }

    // ---------------------------------------------------------------- validaciones

    [Fact]
    public async Task Las_validaciones_responden_400_con_su_codigo()
    {
        using var admin = fx.Client("Admin");

        async Task Esperar(HttpResponseMessage r, string codigo)
        {
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Equal(codigo, await r.ErrorCode());
        }

        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", tipo: "raro")), "TIPO_TERMINO_INVALIDO");
        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", concepto: "campo-inexistente")), "CAMPO_DESTINO_INVALIDO");
        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", tipo: "unidad", concepto: "libras")), "UNIDAD_INVALIDA");
        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", tipo: "valor", concepto: "2_7")), "CONTEXTO_INVALIDO");
        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", contexto: "tipoVehiculo")), "CONTEXTO_NO_APLICA");
        await Esperar(await admin.PostAsJsonAsync(Ruta, Termino("x", clienteId: Guid.NewGuid())), "CLIENTE_NO_ENCONTRADO");
        await Esperar(await admin.GetAsync($"{Ruta}?tipo=raro"), "TIPO_TERMINO_INVALIDO");
    }

    [Fact]
    public async Task Un_cuerpo_sin_termino_tipo_o_concepto_es_400_de_validacion()
    {
        using var admin = fx.Client("Admin");

        var r = await admin.PostAsJsonAsync(Ruta, new { tipo = "columna", concepto = "origen" });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Las_opciones_traen_tipos_campos_unidades_y_contextos()
    {
        using var c = fx.Client("Comercial");

        var o = await Cuerpo(await c.GetAsync($"{Ruta}/opciones"));

        Assert.Equal(["columna", "unidad", "valor"], o.GetProperty("tipos").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("origen", o.GetProperty("camposDestino").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(["kg", "t"], o.GetProperty("unidades").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("tipoVehiculo", o.GetProperty("contextos").EnumerateArray().Select(x => x.GetString()));
    }
}
