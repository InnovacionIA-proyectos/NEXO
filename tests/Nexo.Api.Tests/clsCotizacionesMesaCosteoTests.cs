using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-20.1/#286 de extremo a extremo (BD real de pruebas): GET de cotizaciÃ³n por solicitud
/// sin efecto secundario, editar un flete manual sobre la MISMA lÃ­nea, y agregar/quitar
/// servicios conexos mientras la cotizaciÃ³n no tenga resultado.
/// </summary>
[Collection("api")]
public class clsCotizacionesMesaCosteoTests
{
    private readonly clsApiFixture _fx;
    public clsCotizacionesMesaCosteoTests(clsApiFixture fx) => _fx = fx;

    private async Task<(Guid solicitud, Guid linea)> NuevaSolicitudConLinea(HttpClient? cli = null, decimal? valorMercancia = null)
    {
        cli ??= _fx.Client();
        var crear = await cli.PostJson("/api/solicitudes", new
        {
            clienteId = _fx.AnyClienteId(),
            tipo = "spot",
            linea = new { tipologia = "CargaSeca", peso = 10000, valorMercancia },
        });
        var sid = (await crear.Json()).GetProperty("id").GetGuid();
        var detalle = await cli.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{sid}");
        return (sid, detalle.GetProperty("lineas")[0].GetProperty("lineaId").GetGuid());
    }

    /// <summary>CotizaciÃ³n con 1 flete manual de 1.000.000 â†’ (cotizacion, lÃ­neaSolicitud, filaDeCotizaciÃ³n).</summary>
    private async Task<(Guid cotizacion, Guid lineaSolicitud, Guid cotizacionLinea)> CotizacionConFleteManual(HttpClient? cli = null)
    {
        cli ??= _fx.Client();
        var (sid, lid) = await NuevaSolicitudConLinea(cli);
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        var resp = await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_000_000, fuente = "manual" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (cid, lid, (await resp.Json()).GetProperty("lineas")[0].GetProperty("id").GetGuid());
    }

    private async Task<Guid> AgregarConexo(HttpClient cli, Guid cid, Guid cotizacionLinea, decimal cantidad = 1)
    {
        var r = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId = cotizacionLinea, servicioConexoId = _fx.AnyServicioActivoId(), cantidad } } });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var conexos = (await r.Json()).GetProperty("conexos");
        return conexos[conexos.GetArrayLength() - 1].GetProperty("id").GetGuid();
    }

    // ----------------------------------------------------------------- valor de mercancÃ­a

    [Fact]
    public async Task El_GET_del_costeo_trae_valorMercancia_por_linea_o_null_si_no_se_capturo()
    {
        var cli = _fx.Client();
        var (conValor, _) = await NuevaSolicitudConLinea(cli, valorMercancia: 85_000_000m);
        var (sinValor, _) = await NuevaSolicitudConLinea(cli);

        var a = (await cli.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{conValor}")).GetProperty("lineas")[0];
        var b = (await cli.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{sinValor}")).GetProperty("lineas")[0];

        Assert.Equal(85_000_000m, a.GetProperty("valorMercancia").GetDecimal());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("valorMercancia").ValueKind);
    }

    // ----------------------------------------------------------------- GET por solicitud

    [Fact]
    public async Task Por_solicitud_sin_cotizacion_devuelve_204_y_NO_crea_ninguna()
    {
        var cli = _fx.Client();
        var (sid, _) = await NuevaSolicitudConLinea(cli);
        var antes = _fx.WithDb(db => db.Cotizaciones.Count(c => c.SolicitudId == sid));

        var resp = await cli.GetAsync($"/api/cotizaciones/por-solicitud/{sid}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        Assert.Equal(0, antes);
        Assert.Equal(0, _fx.WithDb(db => db.Cotizaciones.Count(c => c.SolicitudId == sid)));
    }

    [Fact]
    public async Task Por_solicitud_con_cotizacion_devuelve_200_con_la_misma_que_el_POST()
    {
        var cli = _fx.Client();
        var (cid, _, _) = await CotizacionConFleteManual(cli);
        var sid = _fx.WithDb(db => db.Cotizaciones.First(c => c.Id == cid).SolicitudId);

        var resp = await cli.GetAsync($"/api/cotizaciones/por-solicitud/{sid}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(cid, dto.GetProperty("id").GetGuid());
        Assert.Equal(1, dto.GetProperty("lineas").GetArrayLength());
    }

    [Fact]
    public async Task Por_solicitud_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync($"/api/cotizaciones/por-solicitud/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ----------------------------------------------------------------- editar flete manual

    [Fact]
    public async Task Editar_un_flete_manual_actualiza_la_MISMA_linea_sin_duplicar_ni_inflar_el_total()
    {
        var cli = _fx.Client();
        var (cid, lid, filaId) = await CotizacionConFleteManual(cli);

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000, confirmarSinPiso = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal(1, dto.GetProperty("lineas").GetArrayLength());
        Assert.Equal(filaId, dto.GetProperty("lineas")[0].GetProperty("id").GetGuid());
        Assert.Equal(1_250_000m, dto.GetProperty("lineas")[0].GetProperty("valorFlete").GetDecimal());
        Assert.Equal(1_250_000m, dto.GetProperty("total").GetDecimal());   // no 2.250.000
        Assert.Equal("Manual", dto.GetProperty("lineas")[0].GetProperty("fuente").GetString());
    }

    [Fact]
    public async Task Editar_el_flete_de_una_cotizacion_entregada_devuelve_409_COTIZACION_NO_EDITABLE()
    {
        var cli = _fx.Client();
        var (cid, lid, _) = await CotizacionConFleteManual(cli);
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000 });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("COTIZACION_NO_EDITABLE", await resp.ErrorCode());
    }

    // S6-HU-26.1/#30: antes un flete que no era Manual respondía 409 FLETE_NO_EDITABLE. Ahora se edita (queda Manual).
    [Fact]
    public async Task Editar_un_flete_de_plaza_lo_actualiza_y_la_fuente_pasa_a_Manual()
    {
        var cli = _fx.Client();
        var (sid, lid) = await NuevaSolicitudConLinea(cli);
        var cid = (await (await cli.PostJson("/api/cotizaciones", new { solicitudId = sid })).Json()).GetProperty("id").GetGuid();
        await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_000_000, fuente = "tarifario" });

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000, confirmarSinPiso = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var linea = (await resp.Json()).GetProperty("lineas")[0];
        Assert.Equal(1_250_000m, linea.GetProperty("valorFlete").GetDecimal());
        Assert.Equal("Manual", linea.GetProperty("fuente").GetString());
    }

    // S6-HU-26.1/#30: la línea sintética no tiene ruta, así que no hay piso que verificar. Sin confirmar, NO se guarda en silencio.
    [Fact]
    public async Task Editar_sin_poder_verificar_el_piso_devuelve_409_PISO_SICETAC_NO_VERIFICABLE_y_no_cambia_nada()
    {
        var cli = _fx.Client();
        var (cid, lid, _) = await CotizacionConFleteManual(cli);

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000 });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("PISO_SICETAC_NO_VERIFICABLE", await resp.ErrorCode());
        var dto = await (await cli.GetAsync($"/api/cotizaciones/{cid}")).Json();
        Assert.Equal(1_000_000m, dto.GetProperty("lineas")[0].GetProperty("valorFlete").GetDecimal());   // el valor de partida
    }

    [Fact]
    public async Task Editar_una_linea_que_no_esta_en_la_cotizacion_devuelve_404_LINEA_NO_ENCONTRADA()
    {
        var cli = _fx.Client();
        var (cid, _, _) = await CotizacionConFleteManual(cli);

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{Guid.NewGuid()}", new { valorFlete = 1_250_000 });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("LINEA_NO_ENCONTRADA", await resp.ErrorCode());
    }

    [Fact]
    public async Task Editar_con_valor_cero_devuelve_400()
    {
        var cli = _fx.Client();
        var (cid, lid, _) = await CotizacionConFleteManual(cli);

        var resp = await cli.PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("ServiciosLogisticos")]
    public async Task Editar_el_flete_con_un_rol_no_autorizado_devuelve_403(string rol)
    {
        var (cid, lid, _) = await CotizacionConFleteManual();

        var resp = await _fx.Client(rol).PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000 });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Theory]
    [InlineData("AdminTarifas")]
    [InlineData("CoordinadorFletes")]
    public async Task Editar_el_flete_con_AdminTarifas_o_CoordinadorFletes_devuelve_200(string rol)
    {
        var (cid, lid, _) = await CotizacionConFleteManual();

        var resp = await _fx.Client(rol).PutJson($"/api/cotizaciones/{cid}/lineas/{lid}", new { valorFlete = 1_250_000, confirmarSinPiso = true });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ----------------------------------------------------------------- conexos: roles

    [Theory]
    [InlineData("Comercial")]
    [InlineData("AdminTarifas")]
    [InlineData("CoordinadorFletes")]
    [InlineData("Admin")]
    public async Task Agregar_y_quitar_conexos_lo_pueden_hacer_los_4_roles(string rol)
    {
        var (cid, _, filaId) = await CotizacionConFleteManual();
        var cli = _fx.Client(rol);

        var conexoId = await AgregarConexo(cli, cid, filaId);
        var quitar = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");

        Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        Assert.Equal(0, (await quitar.Json()).GetProperty("conexos").GetArrayLength());
    }

    [Fact]
    public async Task Agregar_o_quitar_conexos_sin_rol_autorizado_devuelve_403()
    {
        var (cid, _, filaId) = await CotizacionConFleteManual();
        var conexoId = await AgregarConexo(_fx.Client(), cid, filaId);
        var cli = _fx.Client("ServiciosLogisticos");

        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId = filaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1 } } });
        var quitar = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");

        Assert.Equal(HttpStatusCode.Forbidden, agregar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, quitar.StatusCode);
    }

    // ----------------------------------------------------------------- conexos tras la entrega

    [Fact]
    public async Task Tras_entregar_se_pueden_agregar_y_quitar_conexos_y_queda_la_marca_sin_mover_Estado_ni_EntregadaAt()
    {
        var cli = _fx.Client();
        var (cid, _, filaId) = await CotizacionConFleteManual(cli);
        var entregada = await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        var entregadaAt = (await entregada.Json()).GetProperty("cotizacion").GetProperty("entregadaAt").GetDateTimeOffset();
        var totalAntes = (await cli.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}")).GetProperty("total").GetDecimal();

        var conexoId = await AgregarConexo(cli, cid, filaId, cantidad: 2);
        var tras = await cli.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}");
        Assert.True(tras.GetProperty("total").GetDecimal() > totalAntes);
        Assert.Equal("Entregada", tras.GetProperty("estado").GetString());
        Assert.Equal(entregadaAt, tras.GetProperty("entregadaAt").GetDateTimeOffset());
        Assert.NotEqual(JsonValueKind.Null, tras.GetProperty("modificadaTrasEntregaAt").ValueKind);

        var quitar = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");
        Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        Assert.Equal(totalAntes, (await quitar.Json()).GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Antes_de_entregar_modificadaTrasEntregaAt_es_null()
    {
        var cli = _fx.Client();
        var (cid, _, filaId) = await CotizacionConFleteManual(cli);

        await AgregarConexo(cli, cid, filaId);

        var dto = await cli.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}");
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("modificadaTrasEntregaAt").ValueKind);
    }

    [Theory]
    [InlineData("ganada")]
    [InlineData("perdida")]
    public async Task Con_Ganada_o_Perdida_agregar_y_quitar_devuelven_409_COTIZACION_CERRADA(string resultado)
    {
        var cli = _fx.Client();
        var (cid, _, filaId) = await CotizacionConFleteManual(cli);
        var conexoId = await AgregarConexo(cli, cid, filaId);
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);
        var marcar = await _fx.Client("Comercial").PostJson($"/api/cotizaciones/{cid}/resultado", new { resultado });
        Assert.Equal(HttpStatusCode.OK, marcar.StatusCode);

        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/conexos",
            new { items = new[] { new { lineaId = filaId, servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 1 } } });
        var quitar = await cli.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");

        Assert.Equal(HttpStatusCode.Conflict, agregar.StatusCode);
        Assert.Equal("COTIZACION_CERRADA", await agregar.ErrorCode());
        Assert.Equal(HttpStatusCode.Conflict, quitar.StatusCode);
        Assert.Equal("COTIZACION_CERRADA", await quitar.ErrorCode());
        Assert.Equal(1, (await cli.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}")).GetProperty("conexos").GetArrayLength());
    }

    private Guid CrearUsuario()
    {
        var id = Guid.NewGuid();
        _fx.WithDb(db =>
        {
            db.Usuarios.Add(new Nexo.Domain.Usuarios.Entidades.clsUsuario
            {
                Id = id,
                Nombre = "Tarifas de prueba " + id.ToString("N")[..8],
                Email = $"tarifas-prueba-{id:N}@boterosoto.com.co",
                Rol = Nexo.Domain.Enums.RolUsuario.AdminTarifas,
            });
            return db.SaveChanges();
        });
        return id;
    }

    // Camino REAL del aviso (publicador validado + Outbox): si el evento no pasara su
    // validador, o no se pudiera serializar, esta petición fallaría con 500 en vez de 200.
    [Fact]
    public async Task Un_cambio_tras_la_entrega_hecho_por_OTRO_usuario_publica_el_aviso_sin_romper_la_peticion()
    {
        var creador = _fx.Client();                       // usuario de sistema = creador de la solicitud
        var (cid, _, filaId) = await CotizacionConFleteManual(creador);
        await creador.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        var otro = _fx.Client("AdminTarifas", CrearUsuario());
        var conexoId = await AgregarConexo(otro, cid, filaId, cantidad: 3);
        var quitar = await otro.DeleteAsync($"/api/cotizaciones/{cid}/conexos/{conexoId}");

        Assert.Equal(HttpStatusCode.OK, quitar.StatusCode);
        var dto = await creador.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}");
        Assert.NotEqual(JsonValueKind.Null, dto.GetProperty("modificadaTrasEntregaAt").ValueKind);
        Assert.Equal("Entregada", dto.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Un_cambio_tras_la_entrega_hecho_por_el_propio_creador_tambien_deja_la_marca()
    {
        var cli = _fx.Client();
        var (cid, _, filaId) = await CotizacionConFleteManual(cli);
        await cli.PostAsync($"/api/cotizaciones/{cid}/entregar", null);

        await AgregarConexo(cli, cid, filaId);

        var dto = await cli.GetFromJsonAsync<JsonElement>($"/api/cotizaciones/{cid}");
        Assert.NotEqual(JsonValueKind.Null, dto.GetProperty("modificadaTrasEntregaAt").ValueKind);
    }

    [Fact]
    public async Task Quitar_un_conexo_de_otra_cotizacion_devuelve_404_CONEXO_NO_ENCONTRADO()
    {
        var cli = _fx.Client();
        var (cidA, _, _) = await CotizacionConFleteManual(cli);
        var (cidB, _, filaB) = await CotizacionConFleteManual(cli);
        var conexoDeB = await AgregarConexo(cli, cidB, filaB);

        var resp = await cli.DeleteAsync($"/api/cotizaciones/{cidA}/conexos/{conexoDeB}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("CONEXO_NO_ENCONTRADO", await resp.ErrorCode());
    }
}
