using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests.Cubicacion;

/// <summary>
/// #302 (hallazgo de QA sobre #300/PR #301): cobertura directa de los roles de <c>POST</c> y <c>GET /api/cubicacion</c>.
/// #300 agregó AdminTarifas a la lista; sin esta prueba un cambio accidental de esa lista pasaba sin que la suite lo notara.
///
/// Es solo de AUTORIZACIÓN y no llama al Cubicador: con un cuerpo/consulta incompletos el controlador responde 400
/// ANTES de salir a la red, así que "el rol pasó" se lee como 400 (y "no pasó" como 401/403). Corre en cualquier
/// ambiente, sin las variables <c>Cubicador3D__*</c>.
/// </summary>
[Collection("api")]
public class clsCubicacionRolesTests(clsApiFixture fixture)
{
    private static readonly object CuerpoSinMercancias = new { idLineaSolicitud = "LIN-ROLES", mercancias = Array.Empty<object>() };

    // Los 4 roles con permiso. AdminTarifas es el que agregó #300.
    [Theory]
    [InlineData("Comercial")]
    [InlineData("CoordinadorFletes")]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task Los_roles_con_permiso_pasan_la_autorizacion_en_POST(string rol)
    {
        var respuesta = await fixture.Client(rol).PostAsJsonAsync("/api/cubicacion", CuerpoSinMercancias);

        // 400 = autorizó y el controlador rechazó el cuerpo vacío (sin llamar al Cubicador).
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<Nexo.Api.Common.Errors.clsApiError>();
        Assert.Equal("MERCANCIAS_REQUERIDAS", error!.Code);
    }

    [Theory]
    [InlineData("Comercial")]
    [InlineData("CoordinadorFletes")]
    [InlineData("AdminTarifas")]
    [InlineData("Admin")]
    public async Task Los_roles_con_permiso_pasan_la_autorizacion_en_GET(string rol)
    {
        var respuesta = await fixture.Client(rol).GetAsync("/api/cubicacion");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<Nexo.Api.Common.Errors.clsApiError>();
        Assert.Equal("PARAMETROS_REQUERIDOS", error!.Code);
    }

    // Los que NO deben poder: siguen igual que antes de #300.
    [Theory]
    [InlineData("ServiciosLogisticos")]
    [InlineData("LiderProceso")]
    public async Task Los_roles_sin_permiso_reciben_403_en_POST_y_GET(string rol)
    {
        var cliente = fixture.Client(rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/cubicacion", CuerpoSinMercancias)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/cubicacion")).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_recibe_401_en_POST_y_GET()
    {
        var anonimo = fixture.Anon();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync("/api/cubicacion", CuerpoSinMercancias)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/cubicacion")).StatusCode);
    }
}
