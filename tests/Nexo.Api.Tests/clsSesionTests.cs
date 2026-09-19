using System.Net;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

// S2-HU-03.1/03.2. Ejercita el mecanismo end-to-end (autenticación + autorización) a
// través del harness de pruebas (clsTestAuthHandler, X-Test-Role) — no la fuente real del
// portal, que no existe en este entorno. El body JSON exacto de 401/403 que produce
// clsIdentidadPortalAuthenticationHandler (ver ese archivo) no lo ejercita este test: el
// harness usa su propio esquema por diseño (Infra/clsTestAuthHandler.cs).
[Collection("api")]
public class clsSesionTests(clsApiFixture fx)
{
    [Fact]
    public async Task Con_rol_valido_devuelve_la_sesion_resuelta()
    {
        var resp = await fx.Client("AdminTarifas").GetAsync("/api/sesion/actual");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Json();
        Assert.Equal("AdminTarifas", cuerpo.GetProperty("rol").GetString());
        Assert.True(cuerpo.GetProperty("capacidades").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Sin_identidad_del_portal_no_entra()
    {
        var resp = await fx.Anon().GetAsync("/api/sesion/actual");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Con_identidad_pero_sin_rol_de_Nexo_queda_sin_permiso()
    {
        // Autenticado (el esquema de prueba acepta cualquier valor no vacío), pero
        // "RolQueNoExisteEnNexo" no es ninguno de los 6 — la FallbackPolicy debe
        // rechazarlo en TODO endpoint, no solo en los que restringen por rol específico.
        var resp = await fx.Client("RolQueNoExisteEnNexo").GetAsync("/api/sesion/actual");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
