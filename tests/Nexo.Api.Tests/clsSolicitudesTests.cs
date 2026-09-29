using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

[Collection("api")]
public class clsSolicitudesTests
{
    private readonly clsApiFixture _fx;
    public clsSolicitudesTests(clsApiFixture fx) => _fx = fx;

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

    // 6) Aislamiento por comercial (issue #199, S4-HU-19.1)
    //
    // CreadoPor tiene FK a tbl_usuarios: hace falta un usuario real (no un Guid al
    // azar) para poder crear una solicitud "como" él. La creación en sí se hace con
    // el rol Admin (POST /api/solicitudes exige CoordinadorFletes/Admin) pero
    // simulando el Id del comercial vía X-Test-UserId, para que CreadoPor quede en
    // ese Id — lo que se prueba acá es el aislamiento en LECTURA/escritura puntual,
    // no el alta (ese flujo real de Comercial es POST /api/captura-solicitudes,
    // fuera de alcance de esta prueba).

    private Guid CrearUsuarioComercialDePrueba()
    {
        var id = Guid.NewGuid();
        _fx.WithDb(db =>
        {
            db.Usuarios.Add(new Nexo.Domain.Usuarios.Entidades.clsUsuario
            {
                Id = id,
                Nombre = "Comercial de prueba " + id.ToString("N")[..8],
                Email = $"comercial-prueba-{id:N}@boterosoto.com.co",
                Rol = Nexo.Domain.Enums.RolUsuario.Comercial,
            });
            return db.SaveChanges();
        });
        return id;
    }

    private async Task<Guid> CrearComoAsync(Guid comercialId)
    {
        var resp = await _fx.Client("Admin", comercialId).PostJson("/api/solicitudes",
            new { clienteId = _fx.AnyClienteId(), tipo = "spot" });
        return (await resp.Json()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Un_comercial_no_ve_en_la_bandeja_las_solicitudes_de_otro_comercial()
    {
        var comercialA = CrearUsuarioComercialDePrueba();
        var comercialB = CrearUsuarioComercialDePrueba();
        var idDeA = await CrearComoAsync(comercialA);

        var bandejaDeA = await _fx.Client("Comercial", comercialA).GetFromJsonAsync<JsonElement>("/api/solicitudes?atencion=todas");
        var bandejaDeB = await _fx.Client("Comercial", comercialB).GetFromJsonAsync<JsonElement>("/api/solicitudes?atencion=todas");

        Assert.Contains(bandejaDeA.GetProperty("items").EnumerateArray(), it => it.GetProperty("id").GetGuid() == idDeA);
        Assert.DoesNotContain(bandejaDeB.GetProperty("items").EnumerateArray(), it => it.GetProperty("id").GetGuid() == idDeA);
    }

    [Fact]
    public async Task Coordinador_de_fletes_sigue_viendo_el_costeo_de_cualquier_comercial()
    {
        // Verifica acceso directo por Id en vez de listarla en la Bandeja: la Bandeja
        // pagina y ordena por score, así que una solicitud recién creada (score bajo)
        // puede no caer en la página por defecto en una base compartida con cientos de
        // solicitudes — no es lo que este caso necesita probar (eso ya lo cubre el test
        // de aislamiento del comercial, que sí filtra por dueño en vez de por score).
        var idDeA = await CrearComoAsync(CrearUsuarioComercialDePrueba());

        var resp = await _fx.Client("CoordinadorFletes").GetAsync($"/api/solicitudes/{idDeA}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Un_comercial_recibe_403_al_pedir_el_costeo_de_una_solicitud_ajena()
    {
        var comercialA = CrearUsuarioComercialDePrueba();
        var comercialB = CrearUsuarioComercialDePrueba();
        var id = await CrearComoAsync(comercialA);

        var resp = await _fx.Client("Comercial", comercialB).GetAsync($"/api/solicitudes/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("SOLICITUD_AJENA", await resp.ErrorCode());
    }

    [Fact]
    public async Task El_dueno_si_puede_pedir_el_costeo_de_su_propia_solicitud()
    {
        var comercialA = CrearUsuarioComercialDePrueba();
        var id = await CrearComoAsync(comercialA);

        var resp = await _fx.Client("Comercial", comercialA).GetAsync($"/api/solicitudes/{id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Un_comercial_recibe_403_al_rechazar_una_solicitud_ajena()
    {
        var comercialA = CrearUsuarioComercialDePrueba();
        var comercialB = CrearUsuarioComercialDePrueba();
        var id = await CrearComoAsync(comercialA);

        var resp = await _fx.Client("Comercial", comercialB).PostJson($"/api/solicitudes/{id}/rechazar", new { motivo = "precio" });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
