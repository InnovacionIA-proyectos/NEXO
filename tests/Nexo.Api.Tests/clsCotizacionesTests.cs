using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsCotizacionesTests
{
    private readonly clsApiFixture _fx;
    public clsCotizacionesTests(clsApiFixture fx) => _fx = fx;

    /// <summary>Crea una solicitud spot con una línea y devuelve (solicitudId, lineaId).</summary>
    private async Task<(Guid solicitud, Guid linea)> NuevaSolicitudConLinea()
    {
        var cli = _fx.Client();
        var crear = await cli.PostJson("/api/solicitudes", new
        {
            clienteId = _fx.AnyClienteId(),
            tipo = "spot",
            linea = new { tipologia = "CargaSeca", peso = 10000 }
        });
        var sid = (await crear.Json()).GetProperty("id").GetGuid();
        var detalle = await cli.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{sid}");
        var lid = detalle.GetProperty("lineas")[0].GetProperty("lineaId").GetGuid();
        return (sid, lid);
    }

    // 1) Happy path — flujo completo (crear -> flete -> conexos -> entregar -> resultado)
    [Fact]
    public async Task Flujo_completo_cotizacion()
    {
        var cli = _fx.Client();
        var (sid, lid) = await NuevaSolicitudConLinea();

        var cot = await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json();
        var cid = cot.GetProperty("id").GetGuid();

        var lineaResp = await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_850_000, fuente = "sicetac" });
        Assert.Equal(HttpStatusCode.OK, lineaResp.StatusCode);
        // S4-HU-15.4/#250: el id de la línea DE LA COTIZACIÓN (no el de clsLineaSolicitud,
        // "lid") — es el que ahora exige /conexos.
        var cotizacionLineaId = (await lineaResp.Json()).GetProperty("lineas")[0].GetProperty("id").GetGuid();

        var conResp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId = cotizacionLineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 2 } } });
        Assert.Equal(HttpStatusCode.OK, conResp.StatusCode);
        Assert.True((await conResp.Json()).GetProperty("total").GetDecimal() > 0);

        var entregar = await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        Assert.Equal(HttpStatusCode.OK, entregar.StatusCode);
        // /entregar devuelve clsEntregarCotizacionResultDto: el estado va anidado en 'cotizacion'.
        Assert.Equal("Entregada", (await entregar.Json()).GetProperty("cotizacion").GetProperty("estado").GetString());

        var resultado = await cli.PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado = "ganada" });
        Assert.Equal(HttpStatusCode.OK, resultado.StatusCode);
        Assert.Equal("Ganada", (await resultado.Json()).GetProperty("resultado").GetString());
    }

    // 2) Validación
    [Fact]
    public async Task Crear_sin_solicitudId_devuelve_400()
    {
        var resp = await _fx.Client().PostJson("/api/cotizaciones", new { });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Resultado_invalido_devuelve_400()
    {
        var (sid, lid) = await NuevaSolicitudConLinea();
        var cli = _fx.Client();
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1000, fuente = "manual" });
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado = "tal_vez" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // 3) Autenticación
    [Fact]
    public async Task Obtener_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync($"/api/cotizaciones/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 4) No encontrado
    [Fact]
    public async Task Obtener_id_inexistente_devuelve_404()
    {
        var resp = await _fx.Client().GetAsync($"/api/cotizaciones/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("COTIZACION_NO_ENCONTRADA", await resp.ErrorCode());
    }

    // 5) Edge cases
    [Fact] // no se puede entregar sin flete
    public async Task Entregar_sin_flete_devuelve_400()
    {
        var (sid, _) = await NuevaSolicitudConLinea();
        var cli = _fx.Client();
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        var resp = await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("COTIZACION_SIN_FLETE", await resp.ErrorCode());
    }

    [Fact] // entregar dos veces -> 409
    public async Task Entregar_dos_veces_devuelve_409()
    {
        var (sid, lid) = await NuevaSolicitudConLinea();
        var cli = _fx.Client();
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1000, fuente = "manual" });
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        var resp = await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact] // marcar ganada realimenta la Biblioteca de Tarifas Ganadas
    public async Task Marcar_ganada_realimenta_biblioteca()
    {
        var antes = _fx.WithDb(db => db.TarifasHistoricas.Count());

        var (sid, lid) = await NuevaSolicitudConLinea();
        var cli = _fx.Client();
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_500_000, fuente = "sicetac" });
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        await cli.PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado = "ganada" });

        var despues = _fx.WithDb(db => db.TarifasHistoricas.Count());
        Assert.True(despues > antes, "Una cotización ganada debe crear una tarifa histórica.");
    }

    // 6) S4-HU-11.1/#213 — solo Comercial y Admin deciden si una cotización se ganó o se perdió.
    [Theory]
    [InlineData("AdminTarifas")]
    [InlineData("AdminBiblioteca")]
    public async Task Marcar_resultado_con_rol_no_autorizado_devuelve_403(string rol)
    {
        var (sid, lid) = await NuevaSolicitudConLinea();
        var admin = _fx.Client();
        var cid = (await (await admin.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await admin.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1000, fuente = "manual" });
        await admin.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var resp = await _fx.Client(rol).PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado = "ganada" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact] // sin regresión: Comercial sigue pudiendo marcar el resultado.
    public async Task Marcar_resultado_con_rol_Comercial_devuelve_200()
    {
        var (sid, lid) = await NuevaSolicitudConLinea();
        var admin = _fx.Client();
        var cid = (await (await admin.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await admin.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1000, fuente = "manual" });
        await admin.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var resp = await _fx.Client("Comercial").PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado = "ganada" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // 7) S5-HU-18/#262 — origen (individual/general), dimensiones del servicio, PATCH/DELETE de conexos.

    private async Task<(Guid cotizacion, Guid cotizacionLineaId)> NuevaCotizacionConLinea()
    {
        var cli = _fx.Client();
        var (sid, lid) = await NuevaSolicitudConLinea();
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        var lineaResp = await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_000_000, fuente = "manual" });
        var cotizacionLineaId = (await lineaResp.Json()).GetProperty("lineas")[0].GetProperty("id").GetGuid();
        return (cid, cotizacionLineaId);
    }

    [Fact]
    public async Task Conexo_sin_origen_explicito_queda_individual_y_trae_las_dimensiones_del_servicio()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 2 } } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var conexo = (await resp.Json()).GetProperty("conexos")[0];
        Assert.Equal("individual", conexo.GetProperty("origen").GetString());
        Assert.True(conexo.GetProperty("servicio").TryGetProperty("origen", out _), "debe traer las dimensiones del servicio");
    }

    [Fact]
    public async Task Conexo_con_origen_general_queda_guardado()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1, origen = "general" } } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        Assert.Equal("general", (await resp.Json()).GetProperty("conexos")[0].GetProperty("origen").GetString());
    }

    [Fact]
    public async Task Conexo_con_cantidad_cero_devuelve_400()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 0 } } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Conexo_con_cantidad_de_mas_de_2_decimales_devuelve_400()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1.234 } } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("CANTIDAD_INVALIDA", await resp.ErrorCode());
    }

    [Fact]
    public async Task Editar_conexo_actualiza_cantidad_y_recalcula_el_total()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1 } } });
        var conexoId = (await agregar.Json()).GetProperty("conexos")[0].GetProperty("id").GetGuid();

        var resp = await cli.PatchJson($"/api/cotizaciones/{cid}/conexos/{conexoId}", new { cantidad = 3 });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(3, dto.GetProperty("conexos")[0].GetProperty("cantidad").GetDecimal());
    }

    [Fact]
    public async Task Eliminar_conexo_lo_quita_de_la_cotizacion()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1 } } });
        var conexoId = (await agregar.Json()).GetProperty("conexos")[0].GetProperty("id").GetGuid();

        var resp = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, (await resp.Json()).GetProperty("conexos").GetArrayLength());
    }

    // 8) S6-HU-38.2/#408 — el conexo se cotiza a su precio de venta; el costo (valorAplicado) queda aparte.

    private async Task<Guid> ServicioConPrecio(decimal costo, decimal? precioVenta)
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new
        {
            codigoServicio = $"PV_{Guid.NewGuid():N}"[..20], tipoServicio = "Precio de venta", costoUnitario = costo, precioVenta,
            unidad = "Por unidad", vigencia = "2026-12-31",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Json()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Un_conexo_con_precio_de_venta_se_cotiza_a_ese_precio_el_total_suma_la_venta_y_no_cambia_si_la_biblioteca_cambia()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var servicioId = await ServicioConPrecio(costo: 50_000, precioVenta: 80_000);

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos", new { items = new[] { new { lineaId, servicioConexoId = servicioId, cantidad = 2 } } });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        var conexo = dto.GetProperty("conexos")[0];
        Assert.Equal((80_000m, 160_000m, 100_000m), (conexo.GetProperty("precioVentaUnitario").GetDecimal(), conexo.GetProperty("valorVenta").GetDecimal(), conexo.GetProperty("valorAplicado").GetDecimal()));
        Assert.Equal(1_160_000m, dto.GetProperty("total").GetDecimal());   // 1.000.000 de flete + la venta, no el costo

        // La biblioteca cambia: el conexo ya agregado conserva el precio con el que se agregó.
        Assert.Equal(HttpStatusCode.OK, (await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{servicioId}", new { precioVenta = 200_000 })).StatusCode);
        var conexoId = conexo.GetProperty("id").GetGuid();
        var editado = await (await cli.PatchJson($"/api/cotizaciones/{cid}/conexos/{conexoId}", new { cantidad = 3 })).Json();
        var despues = editado.GetProperty("conexos")[0];
        Assert.Equal((80_000m, 240_000m), (despues.GetProperty("precioVentaUnitario").GetDecimal(), despues.GetProperty("valorVenta").GetDecimal()));
        Assert.Equal(1_240_000m, editado.GetProperty("total").GetDecimal());

        var leida = await (await cli.GetAsync($"/api/cotizaciones/{cid}")).Json();
        Assert.Equal(240_000m, leida.GetProperty("conexos")[0].GetProperty("valorVenta").GetDecimal());
        Assert.Equal(1_240_000m, leida.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Un_conexo_sin_precio_de_venta_se_cotiza_a_su_costo_y_un_valorAplicado_no_cambia_la_venta()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var sinPrecio = await ServicioConPrecio(costo: 50_000, precioVenta: null);
        var conPrecio = await ServicioConPrecio(costo: 50_000, precioVenta: 80_000);

        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos", new
        {
            items = new object[]
            {
                new { lineaId, servicioConexoId = sinPrecio, cantidad = 1 },
                new { lineaId, servicioConexoId = conPrecio, cantidad = 1, valorAplicado = 12_345 },
            },
        });
        var conexos = (await resp.Json()).GetProperty("conexos").EnumerateArray().ToList();

        var delSinPrecio = conexos.Single(c => c.GetProperty("servicioConexoId").GetGuid() == sinPrecio);
        Assert.Equal((50_000m, 50_000m), (delSinPrecio.GetProperty("precioVentaUnitario").GetDecimal(), delSinPrecio.GetProperty("valorVenta").GetDecimal()));
        var delConPrecio = conexos.Single(c => c.GetProperty("servicioConexoId").GetGuid() == conPrecio);
        Assert.Equal((12_345m, 80_000m), (delConPrecio.GetProperty("valorAplicado").GetDecimal(), delConPrecio.GetProperty("valorVenta").GetDecimal()));
    }

    [Fact]
    public async Task El_Excel_de_la_cotizacion_muestra_el_valor_de_venta_del_conexo_y_no_su_costo()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var servicioId = await ServicioConPrecio(costo: 50_000, precioVenta: 80_000);
        await cli.PostJson($"/api/cotizaciones/{cid}/conexos", new { items = new[] { new { lineaId, servicioConexoId = servicioId, cantidad = 2 } } });

        var resp = await cli.GetAsync($"/api/cotizaciones/{cid}/exportar-excel");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(await resp.Content.ReadAsByteArrayAsync()));
        var numeros = wb.Worksheets.First().CellsUsed().Where(c => c.DataType == ClosedXML.Excel.XLDataType.Number).Select(c => c.GetDouble()).ToList();
        Assert.Contains(160_000d, numeros);
        Assert.DoesNotContain(100_000d, numeros);
    }

    [Fact]
    public void Ningun_conexo_guardado_tiene_el_precio_de_venta_en_cero_mientras_su_costo_es_mayor_que_cero()
    {
        Assert.Equal(0, _fx.WithDb(db => db.CotizacionConexos.Count(c => c.PrecioVentaUnitario <= 0 && c.ValorAplicado > 0)));
    }

    [Fact]
    public async Task Eliminar_conexo_inexistente_devuelve_404()
    {
        var cli = _fx.Client();
        var (cid, _) = await NuevaCotizacionConLinea();

        var resp = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact] // editar la CANTIDAD de un conexo sigue siendo solo Borrador; quitarlo ya se admite tras entregar (S5-HU-20.1/#286).
    public async Task Editar_conexo_en_cotizacion_entregada_devuelve_409()
    {
        var cli = _fx.Client();
        var (cid, lineaId) = await NuevaCotizacionConLinea();
        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1 } } });
        var conexoId = (await agregar.Json()).GetProperty("conexos")[0].GetProperty("id").GetGuid();
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var editar = await cli.PatchJson($"/api/cotizaciones/{cid}/conexos/{conexoId}", new { cantidad = 2 });
        Assert.Equal(HttpStatusCode.Conflict, editar.StatusCode);
    }
}
