using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CabinaFletes.Api.Tests.Infra;

namespace CabinaFletes.Api.Tests;

[Collection("api")]
public class SolicitudesTests
{
    private readonly ApiFixture _fx;
    public SolicitudesTests(ApiFixture fx) => _fx = fx;

    // 1) Happy path
    [Fact]
    public async Task Bandeja_devuelve_solicitudes_ordenadas_por_score()
    {
        // Issue #166: respuesta paginada { items, total }, ya no un array plano.
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/solicitudes?orderBy=score");
        var items = pagina.GetProperty("items");
        Assert.True(pagina.GetProperty("total").GetInt32() > 0);
        Assert.True(items.GetArrayLength() > 0);
        decimal prev = decimal.MaxValue;
        foreach (var it in items.EnumerateArray())
        {
            var score = it.GetProperty("scorePrioridad").GetDecimal();
            Assert.True(score <= prev, "La bandeja debe venir ordenada por score descendente.");
            prev = score;
        }
    }

    [Fact]
    public async Task Crear_solicitud_valida_devuelve_201()
    {
        var resp = await _fx.Client().PostJson("/api/solicitudes", new
        {
            clienteId = _fx.AnyClienteId(),
            tipo = "spot",
            modalidad = "nacional",
            deadline = "2026-06-25T12:00:00Z",
            valorEstimado = 5_000_000,
            probabilidadCierre = 70,
            linea = new { tipologia = "CargaSeca", peso = 12000 }
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    // 2) Validación
    [Fact]
    public async Task Crear_sin_tipo_devuelve_400()
    {
        var resp = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = _fx.AnyClienteId() });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await resp.ErrorCode());
    }

    [Fact]
    public async Task Crear_con_tipo_invalido_devuelve_400()
    {
        var resp = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = _fx.AnyClienteId(), tipo = "XXX" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // 3) Autenticación
    [Fact]
    public async Task Bandeja_sin_token_devuelve_401()
    {
        var resp = await _fx.Anon().GetAsync("/api/solicitudes");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 4) No encontrado
    [Fact]
    public async Task Costeo_de_id_inexistente_devuelve_404()
    {
        var resp = await _fx.Client().GetAsync($"/api/solicitudes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("SOLICITUD_NO_ENCONTRADA", await resp.ErrorCode());
    }

    // 5) Edge cases
    [Fact] // cliente inexistente -> 400 de negocio
    public async Task Crear_con_cliente_inexistente_devuelve_400()
    {
        var resp = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = Guid.NewGuid(), tipo = "spot" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("CLIENTE_INVALIDO", await resp.ErrorCode());
    }

    [Fact] // pliego de licitación: carga masiva de líneas
    public async Task Cargar_lineas_bulk_en_licitacion()
    {
        var crear = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = _fx.AnyClienteId(), tipo = "licitacion" });
        var id = (await crear.Json()).GetProperty("id").GetGuid();

        var resp = await _fx.Client().PostJson($"/api/solicitudes/{id}/lineas:bulk", new
        {
            lineas = new[]
            {
                new { tipologia = "Contenedor", peso = 20000 },
                new { tipologia = "GranelSolido", peso = 30000 },
            }
        });
        // Desde la #15 la carga masiva NO cuesta en el momento: guarda las líneas y
        // devuelve 202 con el identificador del trabajo. La prueba se quedó afirmando
        // el contrato viejo —200 con el conteo— y nadie lo vio porque Api.Tests no se
        // corría completa. Se corrige aquí, junto con la corrida que lo destapó.
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);

        var cuerpo = await resp.Json();
        Assert.NotEqual(Guid.Empty, cuerpo.GetProperty("trabajoId").GetGuid());
        Assert.Equal("Pendiente", cuerpo.GetProperty("estado").GetString());

        // La ruta de respaldo tiene que venir en la respuesta: es con lo que el cliente
        // reconstruye el estado si nunca recibe un aviso.
        Assert.Equal($"/api/trabajos/{cuerpo.GetProperty("trabajoId").GetGuid()}",
            cuerpo.GetProperty("consultarEn").GetString());
    }

    [Fact] // bulk vacío -> 400
    public async Task Cargar_lineas_bulk_vacio_devuelve_400()
    {
        var crear = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = _fx.AnyClienteId(), tipo = "licitacion" });
        var id = (await crear.Json()).GetProperty("id").GetGuid();
        var resp = await _fx.Client().PostJson($"/api/solicitudes/{id}/lineas:bulk", new { lineas = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact] // campos opcionales nulos: igual crea (spot sin deadline/valor/prob)
    public async Task Crear_spot_con_opcionales_nulos_devuelve_201()
    {
        var resp = await _fx.Client().PostJson("/api/solicitudes", new { clienteId = _fx.AnyClienteId(), tipo = "spot" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }
}
