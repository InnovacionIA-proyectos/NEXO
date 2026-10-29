using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-36.1/#371: herramientas de lectura del asistente, contra la API real. Lo que importa acá es la identidad: el rol del usuario
/// decide qué herramientas ve y puede ejecutar, y las reglas de acceso de cada consulta (el aislamiento por comercial de #199) se mantienen
/// aunque la pida el asistente. Las fuentes externas (DWSice, SAP TM) pueden o no estar configuradas en el ambiente de pruebas; las pruebas
/// aceptan las dos situaciones y exigen que en ambas el resultado traiga su fuente.
/// </summary>
[Collection("api")]
public class clsAsistenteHerramientasApiTests(clsApiFixture fx)
{
    private const string Base = "/api/asistente/herramientas";

    private static readonly string[] ParaTodos =
        ["buscar_ciudades_dane", "consultar_historico_sap", "consultar_piso_sicetac", "consultar_servicios_conexos", "consultar_tarifa_plaza_sap"];

    private static string[] Con(params string[] extra) => [.. ParaTodos, .. extra];

    // Quien costea (AdminTarifas y Admin) además tiene las herramientas que proponen cambios.
    private static readonly string[] Proponer =
        ["proponer_agregar_conexos", "proponer_agregar_flete", "proponer_cambiar_vehiculo", "proponer_editar_flete", "proponer_rechazar_linea"];

    public static IEnumerable<object[]> HerramientasPorRol()
    {
        yield return ["Admin", Con(["consultar_aprovisionamiento", "consultar_propuesta_linea", "consultar_tarifa_plaza_nexo", .. Proponer])];
        yield return ["Comercial", Con("consultar_aprovisionamiento", "consultar_propuesta_linea")];
        yield return ["AdminTarifas", Con(["consultar_propuesta_linea", "consultar_tarifa_plaza_nexo", .. Proponer])];
        yield return ["ServiciosLogisticos", Con("consultar_propuesta_linea")];
        yield return ["LiderProceso", Con("consultar_propuesta_linea")];
        yield return ["AdminBiblioteca", Con("consultar_tarifa_plaza_nexo")];
    }

    [Theory]
    [MemberData(nameof(HerramientasPorRol))]
    public async Task Cada_rol_lista_solo_las_herramientas_que_puede_usar(string rol, string[] esperadas)
    {
        var lista = await fx.Client(rol).GetFromJsonAsync<JsonElement>(Base);

        var nombres = lista.EnumerateArray().Select(h => h.GetProperty("nombre").GetString()!).Order().ToArray();
        Assert.Equal(esperadas.Order().ToArray(), nombres);
        foreach (var h in lista.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(h.GetProperty("descripcion").GetString()));
            Assert.Equal("object", h.GetProperty("esquemaEntrada").GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task Sin_sesion_responde_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.Anon().GetAsync(Base)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fx.Anon().PostJson($"{Base}/buscar_ciudades_dane", new { q = "Med" })).StatusCode);
    }

    [Theory]
    [InlineData("Comercial", "consultar_tarifa_plaza_nexo", """{"q":"Medellín"}""")]
    [InlineData("ServiciosLogisticos", "consultar_tarifa_plaza_nexo", """{"q":"Medellín"}""")]
    [InlineData("LiderProceso", "consultar_tarifa_plaza_nexo", """{"q":"Medellín"}""")]
    [InlineData("AdminBiblioteca", "consultar_propuesta_linea", """{"solicitudId":"eeee0000-0000-0000-0000-000000000099","lineaId":"eeee0000-0000-0000-0000-000000000099"}""")]
    [InlineData("AdminBiblioteca", "consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}""")]
    [InlineData("AdminTarifas", "consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}""")]
    [InlineData("ServiciosLogisticos", "consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}""")]
    public async Task Una_herramienta_que_el_rol_no_puede_usar_responde_403(string rol, string herramienta, string cuerpo)
    {
        var resp = await fx.Client(rol).PostAsync($"{Base}/{herramienta}", new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("HERRAMIENTA_NO_PERMITIDA", await resp.ErrorCode());
    }

    [Fact]
    public async Task Una_herramienta_desconocida_responde_400()
    {
        var resp = await fx.Client().PostJson($"{Base}/borrar_todo", new { });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("HERRAMIENTA_DESCONOCIDA", await resp.ErrorCode());
    }

    [Theory]
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín"}""")]
    [InlineData("consultar_tarifa_plaza_sap", """{"top":999,"q":"Medellín"}""")]
    [InlineData("buscar_ciudades_dane", """{}""")]
    [InlineData("consultar_propuesta_linea", """{"solicitudId":"no-es-guid","lineaId":"x"}""")]
    public async Task Una_entrada_invalida_responde_400_con_el_codigo_de_la_herramienta(string herramienta, string cuerpo)
    {
        var resp = await fx.Client().PostAsync($"{Base}/{herramienta}", new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("HERRAMIENTA_ENTRADA_INVALIDA", await resp.ErrorCode());
    }

    [Fact]
    public async Task Los_servicios_conexos_activos_vienen_con_valor_y_fuente()
    {
        var resp = await fx.Client("ServiciosLogisticos").PostJson($"{Base}/consultar_servicios_conexos", new { top = 5 });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Json();
        Assert.True(r.GetProperty("encontrado").GetBoolean());
        Assert.Contains("conexos", r.GetProperty("fuente").GetString());
        var valor = r.GetProperty("valor");
        Assert.InRange(valor.GetProperty("items").GetArrayLength(), 1, 5);
        Assert.True(valor.GetProperty("total").GetInt32() >= 1);
    }

    [Theory]
    [InlineData("consultar_piso_sicetac", """{"origen":"Medellín","destino":"Bogotá"}""")]
    [InlineData("consultar_tarifa_plaza_sap", """{"q":"Medellín"}""")]
    [InlineData("consultar_historico_sap", """{"q":"Medellín"}""")]
    [InlineData("consultar_aprovisionamiento", """{"ciudadLevante":"Cartagena","origen":"Medellín"}""")]
    [InlineData("buscar_ciudades_dane", """{"q":"Medellín"}""")]
    public async Task Las_consultas_a_fuentes_externas_responden_200_con_fuente_y_sin_inventar_cuando_no_hay_dato(string herramienta, string cuerpo)
    {
        var resp = await fx.Client("Admin").PostAsync($"{Base}/{herramienta}", new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Json();
        Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("fuente").GetString()));
        if (r.GetProperty("encontrado").GetBoolean())
            Assert.NotEqual(JsonValueKind.Null, r.GetProperty("valor").ValueKind);
        else
        {
            Assert.Equal(JsonValueKind.Null, r.GetProperty("valor").ValueKind);
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("nota").GetString()));
        }
    }

    // ---- propuesta de una línea: el aislamiento por comercial (#199) también rige para el asistente

    private Guid CrearComercial()
    {
        var id = Guid.NewGuid();
        fx.WithDb(db =>
        {
            db.Usuarios.Add(new Nexo.Domain.Usuarios.Entidades.clsUsuario
            {
                Id = id,
                Nombre = "Comercial de prueba " + id.ToString("N")[..8],
                Email = $"comercial-asistente-{id:N}@boterosoto.com.co",
                Rol = Nexo.Domain.Enums.RolUsuario.Comercial,
            });
            return db.SaveChanges();
        });
        return id;
    }

    /// <summary>Crea una solicitud con una línea a nombre del comercial y devuelve los dos identificadores.</summary>
    private async Task<(Guid SolicitudId, Guid LineaId)> CrearSolicitudDe(Guid comercialId)
    {
        var crear = await fx.Client("Admin", comercialId).PostJson("/api/solicitudes", new
        {
            clienteId = fx.AnyClienteId(),
            tipo = "spot",
            linea = new { tipologia = "CargaSeca", peso = 12000 },
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var solicitudId = (await crear.Json()).GetProperty("id").GetGuid();

        var costeo = await fx.Client("Admin").GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}");
        var lineaId = costeo.GetProperty("lineas")[0].GetProperty("lineaId").GetGuid();
        return (solicitudId, lineaId);
    }

    [Fact]
    public async Task Un_comercial_pide_la_propuesta_de_su_propia_linea_y_trae_valor_motivo_y_fuente()
    {
        var comercial = CrearComercial();
        var (solicitudId, lineaId) = await CrearSolicitudDe(comercial);

        var resp = await fx.Client("Comercial", comercial).PostJson($"{Base}/consultar_propuesta_linea", new { solicitudId, lineaId });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var r = await resp.Json();
        Assert.True(r.GetProperty("encontrado").GetBoolean());
        Assert.Contains("propuesta", r.GetProperty("fuente").GetString());
        var linea = r.GetProperty("valor");
        Assert.Equal(lineaId, linea.GetProperty("lineaId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(linea.GetProperty("justificacion").GetString()));
        Assert.True(linea.TryGetProperty("motivoPendiente", out _), "el motivo de pendiente viaja siempre, aunque sea null");
    }

    [Fact]
    public async Task Un_comercial_no_puede_pedir_por_el_asistente_la_propuesta_de_la_solicitud_de_otro()
    {
        var duenio = CrearComercial();
        var otro = CrearComercial();
        var (solicitudId, lineaId) = await CrearSolicitudDe(duenio);

        var resp = await fx.Client("Comercial", otro).PostJson($"{Base}/consultar_propuesta_linea", new { solicitudId, lineaId });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        Assert.Equal("SOLICITUD_AJENA", await resp.ErrorCode());
    }

    [Fact]
    public async Task El_administrador_de_tarifas_si_ve_la_propuesta_de_la_solicitud_de_cualquier_comercial()
    {
        var comercial = CrearComercial();
        var (solicitudId, lineaId) = await CrearSolicitudDe(comercial);

        var resp = await fx.Client("AdminTarifas").PostJson($"{Base}/consultar_propuesta_linea", new { solicitudId, lineaId });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task La_propuesta_de_una_solicitud_o_una_linea_inexistente_responde_404()
    {
        var comercial = CrearComercial();
        var (solicitudId, _) = await CrearSolicitudDe(comercial);
        var inexistente = Guid.Parse("eeee0000-0000-0000-0000-000000000099");

        var sinSolicitud = await fx.Client("Admin").PostJson($"{Base}/consultar_propuesta_linea", new { solicitudId = inexistente, lineaId = inexistente });
        var sinLinea = await fx.Client("Admin").PostJson($"{Base}/consultar_propuesta_linea", new { solicitudId, lineaId = inexistente });

        Assert.Equal(HttpStatusCode.NotFound, sinSolicitud.StatusCode);
        Assert.Equal("SOLICITUD_NO_ENCONTRADA", await sinSolicitud.ErrorCode());
        Assert.Equal(HttpStatusCode.NotFound, sinLinea.StatusCode);
        Assert.Equal("LINEA_NO_ENCONTRADA", await sinLinea.ErrorCode());
    }
}
