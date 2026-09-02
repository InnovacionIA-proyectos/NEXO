using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CabinaFletes.Api.Tests.Infra;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Domain.Catalogos.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CabinaFletes.Api.Tests.Cubicacion;

/// <summary>
/// GET /api/lineas-solicitud/{id}/cubicacion — puente de solo lectura que el Cubicador
/// (Python) llama para "crear plan desde línea" (ver cubicador/CONTRATO.md sección 7).
/// Autenticación de servicio-a-servicio (CubicadorApiKey), no la sesión del portal.
/// </summary>
[Collection("api")]
public class LineasSolicitudCubicacionControllerTests(ApiFixture fixture)
{
    private const string ApiKey = "test-cubicador-key";

    private HttpClient ClienteConfigurado()
    {
        var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cubicador3D:InboundApiKey"] = ApiKey,
                })));
        return factory.CreateClient();
    }

    private Guid CrearLineaConPosiciones(string dimensionesJson, string origen = "Bogotá", string destino = "Cali")
    {
        return fixture.WithDb(db =>
        {
            var clienteId = db.Clientes.First().Id;
            var usuarioId = db.Usuarios.First().Id;
            // Ruta tiene índice único (Origen, Destino): reutilizar si ya existe en vez de
            // insertar otra, para no chocar entre corridas de este mismo test.
            var ruta = db.Rutas.FirstOrDefault(r => r.Origen == origen && r.Destino == destino)
                ?? new Ruta { Origen = origen, Destino = destino };
            var solicitud = new Solicitud
            {
                ClienteId = clienteId,
                Tipo = TipoSolicitud.Spot,
                CreadoPor = usuarioId,
            };
            var linea = new LineaSolicitud
            {
                SolicitudId = solicitud.Id,
                Ruta = ruta,
                Dimensiones = dimensionesJson,
            };
            db.Solicitudes.Add(solicitud);
            db.LineasSolicitud.Add(linea);
            db.SaveChanges();
            return linea.Id;
        });
    }

    [Fact]
    public async Task Con_Bearer_correcto_devuelve_posiciones_y_ruta()
    {
        var lineaId = CrearLineaConPosiciones(
            """[{"descripcion":"Pallet","cantidad":3,"largoCm":120,"anchoCm":100,"altoCm":100,"pesoKg":500}]""");

        using var client = ClienteConfigurado();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var resp = await client.GetAsync($"/api/lineas-solicitud/{lineaId}/cubicacion");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<LineaParaCubicacionDto>();
        Assert.NotNull(dto);
        Assert.Equal(lineaId, dto!.LineaId);
        Assert.Equal("Bogotá", dto.Origen);
        Assert.Equal("Cali", dto.Destino);
        var posicion = Assert.Single(dto.Posiciones);
        Assert.Equal("Pallet", posicion.Descripcion);
        Assert.Equal(3, posicion.Cantidad);
        Assert.Equal(120, posicion.LargoCm);
        Assert.Equal(500, posicion.PesoKg);
    }

    [Fact]
    public async Task Sin_posiciones_devuelve_lista_vacia_no_null()
    {
        var lineaId = CrearLineaConPosiciones("[]");

        using var client = ClienteConfigurado();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var dto = await client.GetFromJsonAsync<LineaParaCubicacionDto>($"/api/lineas-solicitud/{lineaId}/cubicacion");

        Assert.NotNull(dto);
        Assert.Empty(dto!.Posiciones);
    }

    [Fact]
    public async Task Linea_inexistente_devuelve_404()
    {
        using var client = ClienteConfigurado();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var resp = await client.GetAsync($"/api/lineas-solicitud/{Guid.NewGuid()}/cubicacion");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Sin_Bearer_devuelve_401()
    {
        var lineaId = CrearLineaConPosiciones("[]");

        using var client = ClienteConfigurado();

        var resp = await client.GetAsync($"/api/lineas-solicitud/{lineaId}/cubicacion");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Con_Bearer_incorrecto_devuelve_401()
    {
        var lineaId = CrearLineaConPosiciones("[]");

        using var client = ClienteConfigurado();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "cualquier-otra-cosa");

        var resp = await client.GetAsync($"/api/lineas-solicitud/{lineaId}/cubicacion");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Con_identidad_del_portal_en_vez_del_Bearer_de_servicio_devuelve_401()
    {
        var lineaId = CrearLineaConPosiciones("[]");

        // El esquema de sesión del portal (X-Portal-Usuario-Id / rol de TestAuthHandler)
        // NO debe abrir este endpoint: es un puente de servicio-a-servicio, no de usuario.
        using var client = fixture.Client("Admin");

        var resp = await client.GetAsync($"/api/lineas-solicitud/{lineaId}/cubicacion");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
