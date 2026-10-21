using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsServiciosConexosTests
{
    private readonly clsApiFixture _fx;
    public clsServiciosConexosTests(clsApiFixture fx) => _fx = fx;

    // Payload mínimo válido (S4-HU-15.1/#223): las dimensiones (origen/destino/puerto/
    // ruta/tipoMaquina) son opcionales, se normalizan a "NO APLICA" si no vienen.
    private static object PayloadValido(string codigo, string tipo = "Precinto", decimal costo = 1000, string? categoria = null) => new
    {
        codigoServicio = codigo,
        tipoServicio = tipo,
        costoUnitario = costo,
        unidad = "Por unidad",
        vigencia = "2026-12-31",
        categoria,
    };

    // 1) Happy path — respuesta paginada { items, total }
    [Fact]
    public async Task Listar_devuelve_catalogo_paginado()
    {
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/servicios-conexos?pageSize=5");
        Assert.True(pagina.GetProperty("total").GetInt32() >= 10);       // total real del catálogo sembrado
        Assert.True(pagina.GetProperty("items").GetArrayLength() <= 5);  // acotado por pageSize
    }

    [Fact]
    public async Task Listar_busca_por_tipo_y_filtra_por_categoria()
    {
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/servicios-conexos?q=carg&categoria=logistico");
        foreach (var it in pagina.GetProperty("items").EnumerateArray())
        {
            Assert.Contains("arg", it.GetProperty("tipoServicio").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("logistico", it.GetProperty("categoria").GetString());
        }
    }

    [Fact]
    public async Task Crear_servicio_valido_devuelve_201()
    {
        var resp = await _fx.Client("ServiciosLogisticos").PostJson("/api/servicios-conexos",
            PayloadValido($"COD_{Guid.NewGuid():N}"[..20]));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal("NO APLICA", dto.GetProperty("origen").GetString());
    }

    // 2) Validación
    [Fact]
    public async Task Crear_sin_codigo_ni_tipo_devuelve_400()
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new { costoUnitario = 1000, unidad = "Por unidad", vigencia = "2026-12-31" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await resp.ErrorCode());
    }

    // categoría no reconocida -> 400 con forma de error uniforme.
    [Fact]
    public async Task Crear_con_categoria_invalida_devuelve_400_con_forma_uniforme()
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos",
            PayloadValido($"CAT_{Guid.NewGuid():N}"[..20], categoria: "opcional"));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CATEGORIA_INVALIDA", err.GetProperty("code").GetString());
        Assert.True(err.TryGetProperty("timestamp", out _)); // { code, message, timestamp }
    }

    // 3) Autenticación
    [Fact]
    public async Task Listar_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync("/api/servicios-conexos");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 4) No encontrado
    [Fact]
    public async Task Editar_id_inexistente_devuelve_404()
    {
        var resp = await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{Guid.NewGuid()}", new { costoUnitario = 99 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("SERVICIO_NO_ENCONTRADO", await resp.ErrorCode());
    }

    // 5) Edge cases
    [Fact] // permiso: el coordinador NO puede gestionar el catálogo -> 403
    public async Task Crear_con_rol_no_autorizado_devuelve_403()
    {
        var resp = await _fx.Client("AdminBiblioteca").PostJson("/api/servicios-conexos", PayloadValido("X"));
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact] // misma combinación de las 6 dimensiones -> 409
    public async Task Crear_servicio_duplicado_devuelve_409()
    {
        var codigo = $"DUP_{Guid.NewGuid():N}"[..20];
        await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido(codigo));
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido(codigo));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("SERVICIO_DUPLICADO", await resp.ErrorCode());
    }

    [Fact] // PATCH parcial: solo actualiza los campos enviados
    public async Task Editar_parcial_actualiza_solo_lo_enviado()
    {
        var crear = await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido($"EDIT_{Guid.NewGuid():N}"[..20], costo: 100));
        var id = (await crear.Json()).GetProperty("id").GetGuid();
        var resp = await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{id}", new { activo = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.False(dto.GetProperty("activo").GetBoolean());
        Assert.Equal(100, dto.GetProperty("costoUnitario").GetDecimal()); // se conservó
    }

    // 6) S4-HU-15.3/#249 — exportar catálogo, plantillas por tipo, e importar como upsert.

    [Fact]
    public async Task ExportarExcel_devuelve_el_catalogo_con_las_12_columnas()
    {
        var resp = await _fx.Client().GetAsync("/api/servicios-conexos/exportar-excel");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        var encabezados = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "PUERTO", "RUTA", "TIPO MAQUINA",
            "COSTO UNITARIO", "UNIDAD", "VIGENCIA", "CATEGORIA", "ACTIVO"], encabezados);
        Assert.True(ws.RowsUsed().Count() > 1, "debe traer al menos una fila de datos (catálogo sembrado).");
    }

    [Theory]
    [InlineData("almacenamiento")]
    [InlineData("itr")]
    [InlineData("precinto")]
    [InlineData("acompanamiento")]
    public async Task PlantillaExcel_tipo_valido_devuelve_xlsx_con_encabezado_y_ejemplo(string tipo)
    {
        var resp = await _fx.Client().GetAsync($"/api/servicios-conexos/plantilla-excel/{tipo}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        Assert.Equal("CODIGO", ws.Cell(1, 1).GetString());
        Assert.True(ws.RowsUsed().Count() >= 2, "debe traer la fila de ejemplo.");
    }

    [Fact]
    public async Task PlantillaExcel_tipo_invalido_devuelve_400()
    {
        var resp = await _fx.Client().GetAsync("/api/servicios-conexos/plantilla-excel/no-existe");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("TIPO_PLANTILLA_INVALIDO", await resp.ErrorCode());
    }

    private static HttpContent ArchivoExcel(string[] encabezados, IEnumerable<string[]> filas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Datos");
        for (var i = 0; i < encabezados.Length; i++) ws.Cell(1, i + 1).Value = encabezados[i];
        var fila = 2;
        foreach (var f in filas)
        {
            for (var i = 0; i < f.Length; i++) ws.Cell(fila, i + 1).Value = f[i];
            fila++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(ms.ToArray());
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", "import.xlsx");
        return contenido;
    }

    [Fact] // reimportar un export previo actualiza en vez de descartar como duplicado.
    public async Task ImportarExcel_fila_existente_actualiza_fila_nueva_crea()
    {
        var admin = _fx.Client("Admin");
        var codigoExistente = $"UPSERT_{Guid.NewGuid():N}"[..20];
        var crear = await admin.PostJson("/api/servicios-conexos", PayloadValido(codigoExistente, costo: 1000));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var codigoNuevo = $"NUEVO_{Guid.NewGuid():N}"[..20];
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD", "VIGENCIA", "CATEGORIA" };
        var archivo = ArchivoExcel(encabezados,
        [
            [codigoExistente, "Precinto", "2000", "Por unidad", "2026-12-31", "adicional"], // actualiza
            [codigoNuevo, "Precinto", "3000", "Por unidad", "2026-12-31", "adicional"],      // crea
        ]);

        var resp = await admin.PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(1, dto.GetProperty("actualizadas").GetInt32());
        Assert.Equal(1, dto.GetProperty("creadas").GetInt32());

        var costoActualizado = _fx.WithDb(db => db.ServiciosConexos.Single(s => s.CodigoServicio == codigoExistente).CostoUnitario);
        Assert.Equal(2000, costoActualizado);
        Assert.True(_fx.WithDb(db => db.ServiciosConexos.Any(s => s.CodigoServicio == codigoNuevo)));
    }

    [Fact]
    public async Task ImportarExcel_fila_repetida_en_el_archivo_cuenta_como_duplicada()
    {
        var codigo = $"FILEDUP_{Guid.NewGuid():N}"[..20];
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados,
        [
            [codigo, "Precinto", "1000", "Por unidad"],
            [codigo, "Precinto", "1500", "Por unidad"], // misma llave otra vez en el mismo archivo
        ]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(1, dto.GetProperty("creadas").GetInt32());
        Assert.Equal(1, dto.GetProperty("duplicadas").GetInt32());
    }

    // S5-HU-19/#261: casos reales reportados por QA contra el ambiente de deploy.

    [Fact] // antes: 500 crudo. "prueba249_precinto" y "PRUEBA249_PRECINTO" son el MISMO
           // servicio para la colación real de la base (insensible a mayúsculas) — ahora
           // también lo son para el importador: la segunda fila actualiza, no choca.
    public async Task ImportarExcel_mismo_codigo_con_distintas_mayusculas_actualiza_no_falla()
    {
        var admin = _fx.Client("Admin");
        var codigo = $"MAY_{Guid.NewGuid():N}"[..15];
        var crear = await admin.PostJson("/api/servicios-conexos", PayloadValido(codigo.ToUpperInvariant(), costo: 1000));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados, [[codigo.ToLowerInvariant(), "Precinto", "2500", "Por unidad"]]);

        var resp = await admin.PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(1, dto.GetProperty("actualizadas").GetInt32());
        Assert.Equal(0, dto.GetProperty("creadas").GetInt32());
    }

    [Fact] // antes: 500 crudo (TIPO SERVICIO es nvarchar(120)). Ahora: fila inválida con
           // detalle, el resto del archivo se sigue procesando.
    public async Task ImportarExcel_texto_largo_queda_invalida_con_detalle_no_500()
    {
        var textoLargo = new string('A', 121);
        var codigoValido = $"OK_{Guid.NewGuid():N}"[..15];
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados,
        [
            [$"LARGO_{Guid.NewGuid():N}"[..15], textoLargo, "1000", "Por unidad"],
            [codigoValido, "Precinto", "1000", "Por unidad"],
        ]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(1, dto.GetProperty("invalidas").GetInt32());
        Assert.Equal(1, dto.GetProperty("creadas").GetInt32()); // la fila válida sí se creó
        var errores = dto.GetProperty("errores");
        Assert.True(errores.GetArrayLength() > 0);
        Assert.Contains("TIPO SERVICIO", errores[0].GetString());
    }

    [Fact] // antes: 500 crudo (decimal(18,2), 16 enteros + 2 decimales).
    public async Task ImportarExcel_monto_con_17_digitos_queda_invalida_no_500()
    {
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados,
            [[$"MONTO_{Guid.NewGuid():N}"[..15], "Precinto", "99999999999999999", "Por unidad"]]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, (await resp.Json()).GetProperty("invalidas").GetInt32());
    }

    [Fact]
    public async Task ImportarExcel_costo_negativo_queda_invalida()
    {
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados, [[$"NEG_{Guid.NewGuid():N}"[..15], "Precinto", "-500", "Por unidad"]]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(1, (await resp.Json()).GetProperty("invalidas").GetInt32());
    }

    [Fact] // antes: una columna ACTIVO presente pero vacía en una fila de actualización
           // reactivaba en silencio un servicio que se había desactivado a propósito.
    public async Task ImportarExcel_activo_vacio_en_actualizacion_no_toca_el_estado_existente()
    {
        var admin = _fx.Client("Admin");
        var codigo = $"ACT_{Guid.NewGuid():N}"[..15];
        var crear = await admin.PostJson("/api/servicios-conexos", PayloadValido(codigo));
        var id = (await crear.Json()).GetProperty("id").GetGuid();
        await admin.PatchJson($"/api/servicios-conexos/{id}", new { activo = false });

        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD", "ACTIVO" };
        var archivo = ArchivoExcel(encabezados, [[codigo, "Precinto", "5000", "Por unidad", ""]]); // ACTIVO presente pero vacío

        var resp = await admin.PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, (await resp.Json()).GetProperty("actualizadas").GetInt32());

        var activo = _fx.WithDb(db => db.ServiciosConexos.Single(s => s.CodigoServicio == codigo).Activo);
        Assert.False(activo, "ACTIVO vacío en el archivo no debe reactivar el servicio.");
    }

    [Theory] // S5-HU-19/#261: cada plantilla trae SOLO sus columnas, no las 12 de siempre.
    [InlineData("almacenamiento", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("itr", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "PUERTO", "TIPO MAQUINA", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("precinto", new[] { "CODIGO", "TIPO SERVICIO", "RUTA", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("acompanamiento", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "COSTO UNITARIO", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    public async Task PlantillaExcel_trae_solo_las_columnas_de_su_tipo(string tipo, string[] columnasEsperadas)
    {
        var resp = await _fx.Client().GetAsync($"/api/servicios-conexos/plantilla-excel/{tipo}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        var columnas = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(columnasEsperadas, columnas);
    }

    [Fact] // la fila 2 (valores fijos) nunca trae datos de apariencia real que se
           // cargarían como un registro si se sube la plantilla sin editar.
    public async Task PlantillaExcel_sin_editar_no_crea_ningun_registro_si_se_reimporta()
    {
        var antes = _fx.WithDb(db => db.ServiciosConexos.Count());

        var plantilla = await _fx.Client().GetAsync("/api/servicios-conexos/plantilla-excel/almacenamiento");
        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(await plantilla.Content.ReadAsByteArrayAsync());
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", "plantilla.xlsx");

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", contenido);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, (await resp.Json()).GetProperty("creadas").GetInt32());

        var despues = _fx.WithDb(db => db.ServiciosConexos.Count());
        Assert.Equal(antes, despues);
    }

    [Fact] // S5-HU-18/#262 (opcional): q también busca por dimensiones del servicio.
    public async Task Listar_busca_por_dimensiones_del_servicio()
    {
        var admin = _fx.Client("Admin");
        var codigo = $"QDIM_{Guid.NewGuid():N}"[..15];
        var ruta = $"RutaUnica-{Guid.NewGuid():N}"[..20];
        var crear = await admin.PostJson("/api/servicios-conexos", new
        {
            codigoServicio = codigo, tipoServicio = "Precinto", ruta,
            costoUnitario = 1000, unidad = "Por unidad", vigencia = "2026-12-31",
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/servicios-conexos?q={ruta}");
        var items = pagina.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(items.EnumerateArray(), i => i.GetProperty("codigoServicio").GetString() == codigo);
    }

    [Fact] // S5-HU-19/#261: exportar-excel ahora admite filtrar por categoría.
    public async Task ExportarExcel_filtra_por_categoria()
    {
        var resp = await _fx.Client().GetAsync("/api/servicios-conexos/exportar-excel?categoria=adicional");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        var colCategoria = ws.Row(1).CellsUsed().Single(c => c.GetString() == "CATEGORIA").Address.ColumnNumber;
        foreach (var fila in ws.RowsUsed().Skip(1))
            Assert.Equal("adicional", fila.Cell(colCategoria).GetString());
    }
}

