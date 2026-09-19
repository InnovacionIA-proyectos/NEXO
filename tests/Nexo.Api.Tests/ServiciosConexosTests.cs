using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class ServiciosConexosTests
{
    private readonly ApiFixture _fx;
    public ServiciosConexosTests(ApiFixture fx) => _fx = fx;

    // 1) Happy path — respuesta paginada { items, total }
    [Fact]
    public async Task Listar_devuelve_catalogo_paginado()
    {
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/servicios-conexos?pageSize=5");
        Assert.True(pagina.GetProperty("total").GetInt32() >= 10);       // total real del catálogo sembrado
        Assert.True(pagina.GetProperty("items").GetArrayLength() <= 5);  // acotado por pageSize
    }

    [Fact]
    public async Task Listar_busca_por_nombre_y_filtra_por_categoria()
    {
        var pagina = await _fx.Client().GetFromJsonAsync<JsonElement>("/api/servicios-conexos?q=carg&categoria=logistico");
        foreach (var it in pagina.GetProperty("items").EnumerateArray())
        {
            Assert.Contains("arg", it.GetProperty("nombre").GetString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("logistico", it.GetProperty("categoria").GetString());
        }
    }

    [Fact]
    public async Task Crear_servicio_valido_devuelve_201()
    {
        var resp = await _fx.Client("ServiciosLogisticos").PostJson("/api/servicios-conexos", new
        {
            nombre = $"Servicio {Guid.NewGuid():N}",
            reglaCobro = "{\"tipo\":\"fijo\",\"valor\":1000}",
            valorBase = 1000
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    // 2) Validación
    [Fact]
    public async Task Crear_sin_nombre_devuelve_400()
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new { valorBase = 1000 });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await resp.ErrorCode());
    }

    // categoría no reconocida -> 400 con forma de error uniforme.
    [Fact]
    public async Task Crear_con_categoria_invalida_devuelve_400_con_forma_uniforme()
    {
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos",
            new { nombre = $"Cat {Guid.NewGuid():N}", categoria = "opcional" });
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
        var resp = await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{Guid.NewGuid()}", new { valorBase = 99 });
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("SERVICIO_NO_ENCONTRADO", await resp.ErrorCode());
    }

    // 5) Edge cases
    [Fact] // permiso: el coordinador NO puede gestionar el catálogo -> 403
    public async Task Crear_con_rol_no_autorizado_devuelve_403()
    {
        var resp = await _fx.Client("CoordinadorFletes").PostJson("/api/servicios-conexos", new { nombre = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact] // nombre duplicado -> 409
    public async Task Crear_servicio_duplicado_devuelve_409()
    {
        var nombre = $"Dup {Guid.NewGuid():N}";
        await _fx.Client("Admin").PostJson("/api/servicios-conexos", new { nombre });
        var resp = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new { nombre });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("SERVICIO_DUPLICADO", await resp.ErrorCode());
    }

    [Fact] // PATCH parcial: solo actualiza los campos enviados
    public async Task Editar_parcial_actualiza_solo_lo_enviado()
    {
        var crear = await _fx.Client("Admin").PostJson("/api/servicios-conexos", new { nombre = $"Edit {Guid.NewGuid():N}", valorBase = 100 });
        var id = (await crear.Json()).GetProperty("id").GetGuid();
        var resp = await _fx.Client("Admin").PatchJson($"/api/servicios-conexos/{id}", new { activo = false });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.False(dto.GetProperty("activo").GetBoolean());
        Assert.Equal(100, dto.GetProperty("valorBase").GetDecimal()); // se conservó
    }
}
