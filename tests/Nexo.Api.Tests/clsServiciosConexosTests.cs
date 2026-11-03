using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsServiciosConexosTests : IDisposable
{
    private readonly clsApiFixture _fx;
    private readonly List<string> _codigos = [];
    private readonly DateTimeOffset _inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

    public clsServiciosConexosTests(clsApiFixture fx) => _fx = fx;

    /// <summary>Un código único para un servicio de esta prueba. Queda registrado y <see cref="Dispose"/> borra el servicio (y su auditoría) al terminar.</summary>
    private string Cod(string prefijo, int largo)
    {
        var codigo = $"{prefijo}_{Guid.NewGuid():N}"[..largo];
        lock (_codigos) _codigos.Add(codigo);
        return codigo;
    }

    /// <summary>
    /// Borra lo que crearon las pruebas de esta clase. Antes quedaba todo en la base compartida (más de mil filas «Precinto») y el listado
    /// paginado dejaba de mostrar las filas nuevas. Es de mejor esfuerzo: un bloqueo ajeno no debe tumbar una prueba que ya pasó.
    /// </summary>
    public void Dispose()
    {
        string[] codigos;
        lock (_codigos) codigos = [.. _codigos];
        try
        {
            _fx.WithDb(db =>
            {
                // Cada importación deja un solo registro de auditoría sin servicio (EntidadId vacío): las de esta clase son las de su ventana.
                db.AuditLogs.Where(a => a.Entidad == "servicios_conexos" && a.Accion == "importar_excel" && a.CreatedAt >= _inicio).ExecuteDelete();
                var ids = db.ServiciosConexos.Where(x => codigos.Contains(x.CodigoServicio)).Select(x => x.Id).ToList();
                db.AuditLogs.Where(a => a.Entidad == "servicios_conexos" && ids.Contains(a.EntidadId!.Value)).ExecuteDelete();
                db.ServiciosConexos.Where(x => ids.Contains(x.Id)
                    && !db.CotizacionConexos.Any(c => c.ServicioConexoId == x.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }

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
            PayloadValido(Cod("COD", 20)));
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
            PayloadValido(Cod("CAT", 20), categoria: "opcional"));
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
        var codigo = Cod("DUP", 20);
        await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido(codigo));
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido(codigo));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("SERVICIO_DUPLICADO", await resp.ErrorCode());
    }

    [Fact] // PATCH parcial: solo actualiza los campos enviados
    public async Task Editar_parcial_actualiza_solo_lo_enviado()
    {
        var crear = await _fx.Client("Admin").PostJson("/api/servicios-conexos", PayloadValido(Cod("EDIT", 20), costo: 100));
        var id = (await crear.Json()).GetProperty("id").GetGuid();
        var resp = await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{id}", new { activo = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.False(dto.GetProperty("activo").GetBoolean());
        Assert.Equal(100, dto.GetProperty("costoUnitario").GetDecimal()); // se conservó
    }

    // 5b) S6-HU-38.2/#408 — precio de venta.

    private async Task<(Guid Id, string Codigo)> CrearConPrecio(decimal costo, decimal? precioVenta)
    {
        var codigo = Cod("PV", 20);
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new
        {
            codigoServicio = codigo, tipoServicio = "Precinto", costoUnitario = costo, precioVenta, unidad = "Por unidad", vigencia = "2026-12-31",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return ((await resp.Json()).GetProperty("id").GetGuid(), codigo);
    }

    private decimal? PrecioVentaDe(string codigo) => _fx.WithDb(db => db.ServiciosConexos.Single(s => s.CodigoServicio == codigo).PrecioVenta);

    [Fact]
    public async Task Crear_con_precio_de_venta_lo_devuelve_y_sin_el_queda_vacio()
    {
        var (conPrecio, codigoConPrecio) = await CrearConPrecio(1000, 1500);
        var (sinPrecio, codigoSinPrecio) = await CrearConPrecio(1000, null);

        // Cada uno se busca por su código exacto: no depende del tamaño de la página ni del orden del listado.
        async Task<JsonElement> Listado(string codigo, Guid id)
        {
            var lista = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/servicios-conexos?q={codigo}");
            return lista.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == id);
        }

        Assert.Equal(1500m, (await Listado(codigoConPrecio, conPrecio)).GetProperty("precioVenta").GetDecimal());
        Assert.Equal(JsonValueKind.Null, (await Listado(codigoSinPrecio, sinPrecio)).GetProperty("precioVenta").ValueKind);
    }

    [Fact]
    public async Task Un_precio_de_venta_negativo_es_400()
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new
        {
            codigoServicio = Cod("PV", 20), tipoServicio = "Precinto", costoUnitario = 1000, precioVenta = -1, unidad = "Por unidad", vigencia = "2026-12-31",
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task El_PATCH_cambia_el_precio_de_venta_no_lo_toca_si_no_viene_y_lo_borra_con_limpiarPrecioVenta()
    {
        var (id, codigo) = await CrearConPrecio(1000, 1500);
        var admin = _fx.Client("Admin");

        Assert.Equal(2000m, (await (await admin.PatchJson($"/api/servicios-conexos/{id}", new { precioVenta = 2000 })).Json()).GetProperty("precioVenta").GetDecimal());

        var sinTocar = await (await admin.PatchJson($"/api/servicios-conexos/{id}", new { costoUnitario = 1100 })).Json();
        Assert.Equal((1100m, 2000m), (sinTocar.GetProperty("costoUnitario").GetDecimal(), sinTocar.GetProperty("precioVenta").GetDecimal()));

        var ambiguo = await admin.PatchJson($"/api/servicios-conexos/{id}", new { precioVenta = 500, limpiarPrecioVenta = true });
        Assert.Equal(HttpStatusCode.BadRequest, ambiguo.StatusCode);
        Assert.Equal("PRECIO_VENTA_AMBIGUO", await ambiguo.ErrorCode());
        Assert.Equal(2000m, PrecioVentaDe(codigo));

        var borrado = await (await admin.PatchJson($"/api/servicios-conexos/{id}", new { limpiarPrecioVenta = true })).Json();
        Assert.Equal(JsonValueKind.Null, borrado.GetProperty("precioVenta").ValueKind);
        Assert.Null(PrecioVentaDe(codigo));

        // Crear y cada cambio de precio quedan en la auditoría con el precio de venta.
        var diffs = _fx.WithDb(db => db.AuditLogs.Where(a => a.Entidad == "servicios_conexos" && a.EntidadId == id).OrderBy(a => a.CreatedAt).Select(a => a.Diff).ToList());
        Assert.Contains(diffs, d => d != null && d.Contains("\"precioVenta\":1500"));
        Assert.Contains(diffs, d => d != null && d.Contains("\"antes\":1500") && d.Contains("\"despues\":2000"));
        Assert.Contains(diffs, d => d != null && d.Contains("\"antes\":2000") && d.Contains("\"despues\":null"));
    }

    [Fact]
    public async Task ImportarExcel_lee_PRECIO_VENTA_y_una_columna_VALOR_VENTA_no_se_confunde_con_el_costo()
    {
        var existente = await CrearConPrecio(1000, null);
        var nuevo = Cod("PVN", 20);
        var archivo = ArchivoExcel(["CODIGO", "TIPO SERVICIO", "VALOR VENTA", "COSTO UNITARIO", "UNIDAD"],
        [
            [existente.Codigo, "Precinto", "1800", "1200", "Por unidad"], // actualiza
            [nuevo, "Precinto", "2500.5", "2000", "Por unidad"],            // crea
        ]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var actualizado = _fx.WithDb(db => db.ServiciosConexos.Single(s => s.CodigoServicio == existente.Codigo));
        Assert.Equal((1200m, 1800m), (actualizado.CostoUnitario, actualizado.PrecioVenta));
        var creado = _fx.WithDb(db => db.ServiciosConexos.Single(s => s.CodigoServicio == nuevo));
        Assert.Equal((2000m, 2500.5m), (creado.CostoUnitario, creado.PrecioVenta));
    }

    [Fact]
    public async Task Reimportar_sin_la_columna_o_con_la_celda_vacia_no_borra_el_precio_de_venta()
    {
        var (_, codigo) = await CrearConPrecio(1000, 1500);
        var admin = _fx.Client("Admin");

        await admin.PostAsync("/api/servicios-conexos/importar-excel", ArchivoExcel(["CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD"], [[codigo, "Precinto", "1100", "Por unidad"]]));
        Assert.Equal(1500m, PrecioVentaDe(codigo));

        await admin.PostAsync("/api/servicios-conexos/importar-excel", ArchivoExcel(["CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "PRECIO VENTA", "UNIDAD"], [[codigo, "Precinto", "1200", "", "Por unidad"]]));
        Assert.Equal(1500m, PrecioVentaDe(codigo));
    }

    [Fact]
    public async Task Un_PRECIO_VENTA_ilegible_o_negativo_invalida_solo_esa_fila()
    {
        var bueno = Cod("PVB", 20);
        var malo = Cod("PVM", 20);
        var negativo = Cod("PVG", 20);
        var archivo = ArchivoExcel(["CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "PRECIO VENTA", "UNIDAD"],
        [
            [bueno, "Precinto", "1000", "1500", "Por unidad"],
            [malo, "Precinto", "1000", "abc", "Por unidad"],
            [negativo, "Precinto", "1000", "-5", "Por unidad"],
        ]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        var dto = await resp.Json();

        Assert.Equal((1, 2), (dto.GetProperty("creadas").GetInt32(), dto.GetProperty("invalidas").GetInt32()));
        var errores = string.Join("|", dto.GetProperty("errores").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("PRECIO VENTA ilegible", errores);
        Assert.Contains("PRECIO VENTA negativo", errores);
        Assert.Equal(1500m, PrecioVentaDe(bueno));
        Assert.False(_fx.WithDb(db => db.ServiciosConexos.Any(s => s.CodigoServicio == malo || s.CodigoServicio == negativo)));
    }

    [Fact]
    public async Task Exportar_escribe_el_precio_de_venta_y_reimportar_el_export_lo_conserva()
    {
        var (_, codigo) = await CrearConPrecio(1000, 1500);
        var admin = _fx.Client("Admin");

        var resp = await admin.GetAsync($"/api/servicios-conexos/exportar-excel?q={codigo}");
        var bytes = await resp.Content.ReadAsByteArrayAsync();
        using (var wb = new XLWorkbook(new MemoryStream(bytes)))
        {
            var ws = wb.Worksheets.First();
            Assert.Equal("PRECIO VENTA", ws.Cell(1, 9).GetString());
            Assert.Equal(1500m, ws.Cell(2, 9).GetValue<decimal>());
        }

        var contenido = new MultipartFormDataContent();
        var parte = new ByteArrayContent(bytes);
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        contenido.Add(parte, "archivo", "export.xlsx");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/servicios-conexos/importar-excel", contenido)).StatusCode);
        Assert.Equal(1500m, PrecioVentaDe(codigo));
    }

    // 6) S4-HU-15.3/#249 — exportar catálogo, plantillas por tipo, e importar como upsert.

    [Fact]
    public async Task ExportarExcel_devuelve_el_catalogo_con_las_13_columnas()
    {
        var resp = await _fx.Client().GetAsync("/api/servicios-conexos/exportar-excel");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var ws = wb.Worksheets.First();
        var encabezados = ws.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Equal(["CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "PUERTO", "RUTA", "TIPO MAQUINA",
            "COSTO UNITARIO", "PRECIO VENTA", "UNIDAD", "VIGENCIA", "CATEGORIA", "ACTIVO"], encabezados);
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
        var codigoExistente = Cod("UPSERT", 20);
        var crear = await admin.PostJson("/api/servicios-conexos", PayloadValido(codigoExistente, costo: 1000));
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);

        var codigoNuevo = Cod("NUEVO", 20);
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
        var codigo = Cod("FILEDUP", 20);
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
        var codigo = Cod("MAY", 15);
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
        var codigoValido = Cod("OK", 15);
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados,
        [
            [Cod("LARGO", 15), textoLargo, "1000", "Por unidad"],
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
            [[Cod("MONTO", 15), "Precinto", "99999999999999999", "Por unidad"]]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(1, (await resp.Json()).GetProperty("invalidas").GetInt32());
    }

    [Fact]
    public async Task ImportarExcel_costo_negativo_queda_invalida()
    {
        var encabezados = new[] { "CODIGO", "TIPO SERVICIO", "COSTO UNITARIO", "UNIDAD" };
        var archivo = ArchivoExcel(encabezados, [[Cod("NEG", 15), "Precinto", "-500", "Por unidad"]]);

        var resp = await _fx.Client("Admin").PostAsync("/api/servicios-conexos/importar-excel", archivo);
        Assert.Equal(1, (await resp.Json()).GetProperty("invalidas").GetInt32());
    }

    [Fact] // antes: una columna ACTIVO presente pero vacía en una fila de actualización
           // reactivaba en silencio un servicio que se había desactivado a propósito.
    public async Task ImportarExcel_activo_vacio_en_actualizacion_no_toca_el_estado_existente()
    {
        var admin = _fx.Client("Admin");
        var codigo = Cod("ACT", 15);
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
    [InlineData("almacenamiento", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("itr", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "PUERTO", "TIPO MAQUINA", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("precinto", new[] { "CODIGO", "TIPO SERVICIO", "RUTA", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
    [InlineData("acompanamiento", new[] { "CODIGO", "TIPO SERVICIO", "ORIGEN", "DESTINO", "COSTO UNITARIO", "PRECIO VENTA", "VIGENCIA", "UNIDAD", "CATEGORIA", "ACTIVO" })]
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
        var codigo = Cod("QDIM", 15);
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

