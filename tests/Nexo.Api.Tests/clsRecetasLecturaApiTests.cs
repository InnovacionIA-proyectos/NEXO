using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.Common.Ports;

namespace Nexo.Api.Tests;

/// <summary>
/// La lectura de Excel por receta contra la API y la base REALES, con el modelo simulado: leer la estructura (con y sin IA), guardar el formato
/// confirmado, reutilizarlo sin llamar a la IA, listar y borrar. Ninguna prueba llama a Anthropic.
/// </summary>
[Collection("api")]
public class clsRecetasLecturaApiTests(clsApiFixture fx)
{
    private const string Base = "/api/captura-solicitudes";

    private static readonly string RecetaJson = """{"version":1,"hojas":[{"hoja":"Tarifas","filasEncabezado":[3]}]}""";

    private static string RespuestaDeLaIa(string resumen = "La tabla empieza en la fila 3.") =>
        $$"""{"receta":{{RecetaJson}},"resumen":"{{resumen}}","dudas":["¿El peso va en toneladas?"]}""";

    private WebApplicationFactory<clsProgram> FabricaConIa(IAsistenteIaPort ia) =>
        fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IAsistenteIaPort>();
            s.AddSingleton(ia);
        }));

    private static HttpClient ClienteCon(WebApplicationFactory<clsProgram> fabrica, string rol)
    {
        var c = fabrica.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    private static string Marca() => $"R{Guid.NewGuid():N}"[..10];

    /// <summary>Un archivo con datos de la empresa arriba, el encabezado en la fila 3 y dos filas de datos; la marca hace único el formato.</summary>
    private static byte[] Excel(string marca, string titulo = "Tarifa")
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Tarifas");
        ws.Cell(1, 1).Value = $"Formulario {marca}";
        ws.Cell(2, 1).Value = "Razón social"; ws.Cell(2, 2).Value = "ACME";
        string[] titulos = [$"Origen {marca}", $"Destino {marca}", "Vehículo", titulo];
        for (var c = 0; c < titulos.Length; c++) ws.Cell(3, c + 1).Value = titulos[c];
        ws.Cell(4, 1).Value = "Bello"; ws.Cell(4, 2).Value = "Neiva"; ws.Cell(4, 3).Value = "Sencillo"; ws.Cell(4, 4).Value = 120;
        ws.Cell(5, 1).Value = "Cali"; ws.Cell(5, 2).Value = "Bogota"; ws.Cell(5, 3).Value = "Tractomula"; ws.Cell(5, 4).Value = 300;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static MultipartFormDataContent Formulario(byte[] excel, Guid? cliente = null, string? instrucciones = null, string? receta = null, string? nombre = null,
        string nombreArchivo = "tarifas.xlsx")
    {
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(excel);
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", nombreArchivo);
        if (cliente is { } id) contenido.Add(new StringContent(id.ToString()), "clienteId");
        if (instrucciones is not null) contenido.Add(new StringContent(instrucciones), "instrucciones");
        if (receta is not null) contenido.Add(new StringContent(receta), "receta");
        if (nombre is not null) contenido.Add(new StringContent(nombre), "nombre");
        return contenido;
    }

    private Guid Cliente() => fx.WithDb(db => db.Clientes.First().Id);

    private void Limpiar(Guid cliente, string marca) =>
        fx.WithDb(db => db.RecetasLectura.Where(r => r.ClienteId == cliente && r.Huella.Contains(marca.ToLowerInvariant())).ExecuteDelete());

    // ---------------------------------------------------------------- leer la estructura

    [Fact]
    public async Task Leer_la_estructura_con_la_IA_devuelve_la_receta_las_tablas_y_el_resumen()
    {
        var ia = clsAsistenteIaFalso.Responde(RespuestaDeLaIa(), clsFuncionIa.ImportacionExcel);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(Marca())));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal("ia", dto.GetProperty("origenReceta").GetString());
        Assert.True(dto.GetProperty("iaUsada").GetBoolean());
        Assert.Equal("La tabla empieza en la fila 3.", dto.GetProperty("resumen").GetString());
        Assert.Equal("¿El peso va en toneladas?", dto.GetProperty("dudas")[0].GetString());
        var tabla = dto.GetProperty("tablas")[0];
        Assert.Equal(2, tabla.GetProperty("totalFilas").GetInt32());
        Assert.Equal(4, tabla.GetProperty("encabezados").GetArrayLength());
        Assert.Equal("Tarifas", dto.GetProperty("hojasDelArchivo")[0].GetProperty("nombre").GetString());
        Assert.Single(ia.Peticiones);
        Assert.Equal(clsFuncionIa.ImportacionExcel, ia.Peticiones[0].Funcion);
    }

    [Fact]
    public async Task Sin_IA_la_lectura_responde_igual_con_una_receta_automatica_y_el_motivo()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(Marca())));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal("automatica", dto.GetProperty("origenReceta").GetString());
        Assert.False(dto.GetProperty("iaUsada").GetBoolean());
        Assert.Equal("IA_DESACTIVADA", dto.GetProperty("motivoSinIa").GetString());
        Assert.True(dto.GetProperty("tablas").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Una_receta_enviada_sin_instrucciones_se_ejecuta_sin_llamar_a_la_IA()
    {
        var ia = clsAsistenteIaFalso.Falla("IA_ERROR", clsFuncionIa.ImportacionExcel);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(Marca()), receta: RecetaJson));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal("manual", (await resp.Json()).GetProperty("origenReceta").GetString());
        Assert.Empty(ia.Peticiones);
    }

    [Fact]
    public async Task Los_archivos_o_recetas_invalidos_responden_400_con_su_codigo()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        var sinArchivo = await c.PostAsync($"{Base}/leer-estructura", new MultipartFormDataContent());
        Assert.Equal(HttpStatusCode.BadRequest, sinArchivo.StatusCode);

        var noEsExcel = await c.PostAsync($"{Base}/leer-estructura", Formulario([1, 2, 3], nombreArchivo: "notas.txt"));
        Assert.Equal(("FORMATO_NO_SOPORTADO", HttpStatusCode.BadRequest), (await noEsExcel.ErrorCode(), noEsExcel.StatusCode));

        var corrupto = await c.PostAsync($"{Base}/leer-estructura", Formulario([1, 2, 3]));
        Assert.Equal(HttpStatusCode.BadRequest, corrupto.StatusCode);

        var recetaMala = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(Marca()), receta: """{"version":1,"hojas":[{"hoja":"Tarifas","filasEncabezado":[0]}]}"""));
        Assert.Equal(HttpStatusCode.BadRequest, recetaMala.StatusCode);
        Assert.Equal("RECETA_INVALIDA", await recetaMala.ErrorCode());
    }

    // ---------------------------------------------------------------- guardar, reutilizar, listar y borrar

    [Fact]
    public async Task Un_formato_guardado_se_reutiliza_sin_IA_aunque_cambien_los_datos_y_si_cambian_los_titulos_vuelve_a_la_IA()
    {
        var cliente = Cliente();
        var marca = Marca();
        var ia = clsAsistenteIaFalso.Responde(RespuestaDeLaIa("Con el formato nuevo."), clsFuncionIa.ImportacionExcel);
        using var fabrica = FabricaConIa(ia);
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var guardar = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), cliente, "El encabezado está en la fila 3", RecetaJson, "Tarifas ACME"));
            Assert.Equal(HttpStatusCode.Created, guardar.StatusCode);
            var guardada = await guardar.Json();
            var id = guardada.GetProperty("id").GetGuid();
            Assert.Equal(("Tarifas ACME", cliente), (guardada.GetProperty("nombre").GetString(), guardada.GetProperty("clienteId").GetGuid()));
            Assert.Equal("Tarifas", guardada.GetProperty("receta").GetProperty("hojas")[0].GetProperty("hoja").GetString());

            // El mismo formato, sin instrucciones: sale de la receta guardada, sin IA.
            var igual = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(marca), cliente));
            var dtoIgual = await igual.Json();
            Assert.Equal("guardada", dtoIgual.GetProperty("origenReceta").GetString());
            Assert.Equal(id, dtoIgual.GetProperty("recetaGuardadaId").GetGuid());
            Assert.Empty(ia.Peticiones);
            Assert.Equal(1, fx.WithDb(db => db.RecetasLectura.Where(r => r.Id == id).Select(r => r.Usos).Single()));

            // Otro título de la tabla: el formato cambió, la IA recibe la receta anterior y lo que cambió.
            var cambiado = await c.PostAsync($"{Base}/leer-estructura", Formulario(Excel(marca, "Valor flete"), cliente));
            var dtoCambiado = await cambiado.Json();
            Assert.Equal("ia", dtoCambiado.GetProperty("origenReceta").GetString());
            Assert.True(dtoCambiado.GetProperty("cambios").GetProperty("nuevos").GetArrayLength() > 0);
            var mensaje = Assert.Single(ia.Peticiones).Mensajes[0].Contenido;
            Assert.Contains("RECETA ANTERIOR", mensaje);
            Assert.Contains("El encabezado está en la fila 3", mensaje);
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Guardar_dos_veces_el_mismo_formato_reemplaza_y_listar_y_borrar_funcionan()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");
        try
        {
            var primera = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), cliente, receta: RecetaJson, nombre: "v1"));
            Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
            var id = (await primera.Json()).GetProperty("id").GetGuid();

            var segunda = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), cliente, receta: RecetaJson, nombre: "v2"));
            Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
            var reemplazada = await segunda.Json();
            Assert.Equal((id, "v2"), (reemplazada.GetProperty("id").GetGuid(), reemplazada.GetProperty("nombre").GetString()));

            var lista = await (await c.GetAsync($"{Base}/recetas-lectura?clienteId={cliente}")).Json();
            Assert.Contains(lista.EnumerateArray(), r => r.GetProperty("id").GetGuid() == id);

            Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Base}/recetas-lectura/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"{Base}/recetas-lectura/{id}")).StatusCode);
            Assert.Equal(0, fx.WithDb(db => db.RecetasLectura.Count(r => r.Id == id)));
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Guardar_rechaza_una_receta_que_no_da_filas_un_cliente_inexistente_y_la_falta_de_cliente()
    {
        var cliente = Cliente();
        var marca = Marca();
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Admin");
        try
        {
            var sinFilas = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), cliente, receta: """{"version":1,"hojas":[{"hoja":"Tarifas","filasEncabezado":[5]}]}"""));
            Assert.Equal((HttpStatusCode.BadRequest, "RECETA_SIN_FILAS"), (sinFilas.StatusCode, await sinFilas.ErrorCode()));

            var inexistente = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), Guid.NewGuid(), receta: RecetaJson));
            Assert.Equal("CLIENTE_NO_ENCONTRADO", await inexistente.ErrorCode());

            var sinCliente = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), receta: RecetaJson));
            Assert.Equal("CLIENTE_REQUERIDO", await sinCliente.ErrorCode());

            var sinReceta = await c.PutAsync($"{Base}/recetas-lectura", Formulario(Excel(marca), cliente));
            Assert.Equal(HttpStatusCode.BadRequest, sinReceta.StatusCode);

            Assert.Equal(0, fx.WithDb(db => db.RecetasLectura.Count(r => r.ClienteId == cliente && r.Huella.Contains(marca.ToLowerInvariant()))));
        }
        finally { Limpiar(cliente, marca); }
    }

    [Fact]
    public async Task Listar_con_un_NIT_sin_cliente_local_responde_lista_vacia()
    {
        using var fabrica = FabricaConIa(clsAsistenteIaFalso.SinConfigurar());
        using var c = ClienteCon(fabrica, "Comercial");

        var resp = await c.GetAsync($"{Base}/recetas-lectura?clienteNit=1");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, (await resp.Json()).GetArrayLength());
    }
}
