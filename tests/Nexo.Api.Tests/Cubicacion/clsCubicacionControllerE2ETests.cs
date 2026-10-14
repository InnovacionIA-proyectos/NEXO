using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Cubicacion.DTOs;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// S5-HU-18.1/#273: contra el Cubicador REAL (docker compose local,
/// cubicador/docker-compose.yml, puerto 8081), no un servidor de mentira — confirma que el
/// contrato que NEXO habla hoy es, de verdad, el que el Cubicador real entiende.
///
/// Requiere, igual que TEST_DB_CONNECTION para el resto de la suite,
/// Cubicador3D__RecomendacionesApiKey/OutboundBaseUrl en el entorno que corre la prueba
/// (ver .env) y el stack de <c>cubicador/docker-compose.yml</c> arriba con el catálogo
/// local sembrado (<c>docker compose exec api python scripts/preparar_catalogo_local.py</c>,
/// más <c>m-CAJAS_CARTON</c> en <c>tbl_tipos_mercancia</c>). Sin eso, falla con un error
/// claro (503/timeout) — no en silencio, mismo criterio que el resto del proyecto.
/// </summary>
[Collection("api")]
public class clsCubicacionControllerE2ETests(clsApiFixture fixture)
{
    [clsHechoConCubicadorReal("Cubicador3D__RecomendacionesApiKey")]
    public async Task Crear_contra_el_Cubicador_real_devuelve_ASIGNADO_con_el_contrato_nuevo()
    {
        var cliente = fixture.Client("Comercial");
        var body = new
        {
            idLineaSolicitud = $"E2E-{Guid.NewGuid():N}",
            mercancias = new[]
            {
                new { idTipoMercancia = "m-CAJAS_CARTON", cantidad = 10, largoCm = 40, anchoCm = 30, altoCm = 30, pesoKg = 15 },
            },
        };

        var respuesta = await cliente.PostAsJsonAsync("/api/cubicacion", body);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<clsRecomendacionDto>();
        Assert.NotNull(dto);
        Assert.Equal("NEXO", dto!.OrigenSistema);
        Assert.Equal("ASIGNADO", dto.Resultado);
        Assert.NotEmpty(dto.Vehiculos);
        Assert.Equal("2L2", dto.Vehiculos[0].CodigoVehiculo);
        // SQL Server devuelve uniqueidentifier en MAYÚSCULAS (doc del Cubicador).
        Assert.Equal(dto.IdRecomendacion.ToUpperInvariant(), dto.IdRecomendacion);
    }

    [clsHechoConCubicadorReal("Cubicador3D__RecomendacionesApiKey")]
    public async Task Consultar_trae_de_vuelta_la_misma_recomendacion_que_se_creo()
    {
        var cliente = fixture.Client("Comercial");
        var idLinea = $"E2E-{Guid.NewGuid():N}";
        var creacion = await cliente.PostAsJsonAsync("/api/cubicacion", new
        {
            idLineaSolicitud = idLinea,
            mercancias = new[]
            {
                new { idTipoMercancia = "m-CAJAS_CARTON", cantidad = 5, largoCm = 40, anchoCm = 30, altoCm = 30, pesoKg = 15 },
            },
        });
        var creada = await creacion.Content.ReadFromJsonAsync<clsRecomendacionDto>();

        var respuesta = await cliente.GetAsync($"/api/cubicacion?idLineaSolicitud={idLinea}&origenSistema=NEXO");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var reconsultada = await respuesta.Content.ReadFromJsonAsync<clsRecomendacionDto>();
        Assert.Equal(creada!.IdRecomendacion, reconsultada!.IdRecomendacion);
    }

    [clsHechoConCubicadorReal("Cubicador3D__RecomendacionesApiKey")]
    public async Task Una_mercancia_desconocida_propaga_el_codigo_real_del_Cubicador_como_400()
    {
        var cliente = fixture.Client("Comercial");
        var respuesta = await cliente.PostAsJsonAsync("/api/cubicacion", new
        {
            idLineaSolicitud = $"E2E-{Guid.NewGuid():N}",
            mercancias = new[]
            {
                new { idTipoMercancia = "no-existe", cantidad = 1, largoCm = 10, anchoCm = 10, altoCm = 10, pesoKg = 1 },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<Nexo.Api.Common.Errors.clsApiError>();
        Assert.Equal("MERCANCIA_DESCONOCIDA", error!.Code);
    }

    [Fact]
    public async Task Sin_mercancias_responde_400_MERCANCIAS_REQUERIDAS_sin_llamar_al_Cubicador()
    {
        var cliente = fixture.Client("Comercial");
        var respuesta = await cliente.PostAsJsonAsync("/api/cubicacion", new
        {
            idLineaSolicitud = "LIN-SIN-MERCANCIAS",
            mercancias = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<Nexo.Api.Common.Errors.clsApiError>();
        Assert.Equal("MERCANCIAS_REQUERIDAS", error!.Code);
    }
}
