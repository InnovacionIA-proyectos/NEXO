using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// S5-HU-18.6/#278: contra el Cubicador REAL (docker compose local,
/// cubicador/docker-compose.yml, puerto 8081) — mismo criterio que
/// clsCubicacionControllerE2ETests. Requiere Cubicador3D__OutboundBaseUrl/OutboundApiKey
/// (la llave GENÉRICA, no RecomendacionesApiKey) en el entorno que corre la prueba.
/// </summary>
[Collection("api")]
public class clsCatalogoCubicadorPassthroughE2ETests(clsApiFixture fixture)
{
    [Fact]
    public async Task GET_categorias_mercancia_con_rol_sin_privilegio_trae_el_catalogo_real()
    {
        // GET no exige rol específico (solo [Authorize]) — un CoordinadorFletes puede leer.
        var cliente = fixture.Client("CoordinadorFletes");

        var respuesta = await cliente.GetAsync("/api/categorias-mercancia");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var categorias = await respuesta.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotNull(categorias);
        Assert.Contains(categorias!, c => c!["nombreCategoria"]!.GetValue<string>() == "GENERAL");
    }

    [Fact]
    public async Task POST_sin_rol_AdminTarifas_responde_403()
    {
        var cliente = fixture.Client("CoordinadorFletes");

        var respuesta = await cliente.PostAsJsonAsync("/api/categorias-mercancia", new { nombreCategoria = "NO_DEBERIA_CREARSE" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_actualizar_y_eliminar_una_carroceria_via_passthrough_contra_el_Cubicador_real()
    {
        var cliente = fixture.Client("AdminTarifas");
        var codigo = $"E2E_{Guid.NewGuid():N}"[..20].ToUpperInvariant();

        // Crear: usuarioId lo estampa NEXO (el del usuario autenticado), no el que mande el body.
        var creado = await cliente.PostAsJsonAsync("/api/tipos-carroceria", new
        {
            codigoCarroceria = codigo,
            nombreCarroceria = "E2E Passthrough",
            usuarioId = "intento-de-suplantacion",
        });
        Assert.Equal(HttpStatusCode.OK, creado.StatusCode);
        var dto = await creado.Content.ReadFromJsonAsync<JsonObject>();
        var id = dto!["id"]!.GetValue<string>();
        Assert.Equal(1, dto["version"]!.GetValue<int>());

        // Actualizar: version correcta.
        var actualizado = await cliente.PutAsJsonAsync($"/api/tipos-carroceria/{id}", new
        {
            version = 1,
            nombreCarroceria = "E2E Passthrough Editado",
            activo = true,
        });
        Assert.Equal(HttpStatusCode.OK, actualizado.StatusCode);
        var dtoActualizado = await actualizado.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("E2E Passthrough Editado", dtoActualizado!["nombreCarroceria"]!.GetValue<string>());
        Assert.Equal(2, dtoActualizado["version"]!.GetValue<int>());

        // Versión vieja -> 409 CONFLICTO_DE_VERSION (concurrencia optimista).
        var conflicto = await cliente.PutAsJsonAsync($"/api/tipos-carroceria/{id}", new { version = 1, nombreCarroceria = "No debería aplicar" });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        var error = await conflicto.Content.ReadFromJsonAsync<Nexo.Api.Common.Errors.clsApiError>();
        Assert.Equal("CONFLICTO_DE_VERSION", error!.Code);

        // DELETE con body (HttpClient no manda body en DeleteAsync — se arma a mano).
        var peticionBorrar = new HttpRequestMessage(HttpMethod.Delete, $"/api/tipos-carroceria/{id}")
        {
            Content = JsonContent.Create(new { version = 2 }),
        };
        var borrado = await cliente.SendAsync(peticionBorrar);
        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);

        // La fila ya no está: un GET del catálogo completo no debería listarla más.
        var listado = await cliente.GetAsync("/api/tipos-carroceria");
        var carrocerias = await listado.Content.ReadFromJsonAsync<JsonArray>();
        Assert.DoesNotContain(carrocerias!, c => c!["id"]!.GetValue<string>() == id);
    }

    [Fact]
    public async Task Republicar_catalogo_de_carrocerias_responde_200_con_el_catalogo_completo()
    {
        var cliente = fixture.Client("AdminTarifas");

        var respuesta = await cliente.PostAsync("/api/tipos-carroceria/republicar-catalogo", content: null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var carrocerias = await respuesta.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotEmpty(carrocerias!);
    }
}
