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

        Assert.Equal(HttpStatusCode.OK,
            (await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lid, valorFlete = 1_850_000, fuente = "sicetac" })).StatusCode);

        var conResp = await cli.PostJson($"/api/cotizaciones/{cid}/conexos", new { items = new[] { new { servicioConexoId = _fx.AnyServicioActivoId(), cantidad = 2 } } });
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
}
