using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-25.1/#307 y S6-HU-25.2/#310: roles POR ENDPOINT, contra la API real.
///
/// Es una prueba de AUTORIZACIÓN y nada más: para cada endpoint y cada uno de los 6 roles, un rol que NO está en la lista recibe 403, y uno
/// que SÍ está pasa la autorización (cualquier respuesta que no sea 401/403: normalmente un 400 por cuerpo vacío o un 404 por un Id que no
/// existe). Las sondas de "sí pasa" usan cuerpos inválidos o Ids inexistentes a propósito, para no crear ni encolar nada. Los DELETE
/// destructivos solo se prueban por el lado del 403.
///
/// Cubre lo que cambió con el rol AdminBiblioteca: entra a la Biblioteca y a /api/cubicacion, y NO a Bandeja, Mesa,
/// cotizaciones ni solicitudes; crear solicitud, cargar líneas y recalcular son solo Admin; AdminTarifas cubica completo (planes de cargue y
/// vehículos). Un cambio accidental en cualquiera de esas listas rompe una fila de esta tabla.
/// </summary>
[Collection("api")]
public class clsRolesPorEndpointTests(clsApiFixture fx)
{
    private static readonly string[] Todos =
        ["Comercial", "AdminBiblioteca", "AdminTarifas", "ServiciosLogisticos", "LiderProceso", "Admin"];

    private static readonly Guid Inexistente = Guid.Parse("eeee0000-0000-0000-0000-000000000099");

    /// <param name="Sondeable">false = solo se verifica el 403 de los roles no permitidos (el endpoint haría algo si se le deja pasar).</param>
    public sealed record clsEndpoint(string Nombre, HttpMethod Metodo, string Url, string[] Permitidos, object? Cuerpo = null, bool Sondeable = true)
    {
        public override string ToString() => $"{Metodo} {Nombre}";
    }

    private static readonly string[] Mesa = ["Comercial", "AdminTarifas", "ServiciosLogisticos", "LiderProceso", "Admin"];
    private static readonly string[] CosteoYCubicador = ["Comercial", "AdminTarifas", "Admin"];
    private static readonly string[] Biblioteca = ["AdminBiblioteca", "AdminTarifas", "Admin"];

    public static IEnumerable<object[]> Endpoints()
    {
        clsEndpoint[] lista =
        [
            // ---- Bandeja y Mesa: AdminBiblioteca NO entra, ni a leer
            new("Bandeja GET /api/solicitudes", HttpMethod.Get, "/api/solicitudes", Mesa),
            new("Mesa GET /api/solicitudes/{id}", HttpMethod.Get, $"/api/solicitudes/{Inexistente}", Mesa),
            new("Mesa GET propuesta-tarifa", HttpMethod.Get, $"/api/solicitudes/{Inexistente}/propuesta-tarifa", Mesa),

            // ---- solicitudes: crear, cargar y recalcular son SOLO Admin (D5)
            new("POST /api/solicitudes", HttpMethod.Post, "/api/solicitudes", ["Admin"], new { }),
            new("POST lineas:bulk", HttpMethod.Post, $"/api/solicitudes/{Inexistente}/lineas:bulk", ["Admin"], new { }),
            new("POST recalcular-score", HttpMethod.Post, $"/api/solicitudes/{Inexistente}/recalcular-score", ["Admin"]),
            new("POST resolver-catalogos", HttpMethod.Post, "/api/solicitudes/resolver-catalogos", ["Comercial", "Admin"], new { }),
            new("POST rechazar", HttpMethod.Post, $"/api/solicitudes/{Inexistente}/rechazar", ["Comercial", "Admin"], new { }),

            // ---- cotizaciones: AdminBiblioteca no entra
            new("Cotizaciones GET /{id}", HttpMethod.Get, $"/api/cotizaciones/{Inexistente}", Mesa),
            new("Cotizaciones POST", HttpMethod.Post, "/api/cotizaciones", CosteoYCubicador, new { }),
            new("Cotizaciones POST lineas/rechazo (#320)", HttpMethod.Post, $"/api/cotizaciones/{Inexistente}/lineas/{Inexistente}/rechazo", ["AdminTarifas", "Admin"], new { motivo = "Sin tarifa" }),
            new("Cotizaciones DELETE lineas/rechazo (#320)", HttpMethod.Delete, $"/api/cotizaciones/{Inexistente}/lineas/{Inexistente}/rechazo", ["AdminTarifas", "Admin"]),
            new("Cotizaciones PUT lineas/vehiculo (#322)", HttpMethod.Put, $"/api/cotizaciones/{Inexistente}/lineas/{Inexistente}/vehiculo", ["AdminTarifas", "Admin"], new { configVehiculoId = Inexistente }),

            // ---- diccionario de alias (#321) y bandeja de normalización (#322): leer es para todos; agregar alias y la bandeja, AdminTarifas/Admin
            new("Alias GET", HttpMethod.Get, "/api/catalogos/alias", Todos),
            new("Alias GET /resolver", HttpMethod.Get, "/api/catalogos/alias/resolver?texto=TB", Todos),
            new("Alias POST", HttpMethod.Post, "/api/catalogos/alias", ["AdminTarifas", "Admin"], new { tipo = "vehiculo", alias = "", codigo = "2L2" }),
            new("Bandeja vehiculos-cuarentena GET", HttpMethod.Get, "/api/catalogos/vehiculos-cuarentena", ["AdminTarifas", "Admin"]),
            new("Bandeja mapear POST", HttpMethod.Post, $"/api/catalogos/vehiculos-cuarentena/{Inexistente}/mapear", ["AdminTarifas", "Admin"], new { codigo = "2L2" }),
            new("Bandeja descartar POST", HttpMethod.Post, $"/api/catalogos/vehiculos-cuarentena/{Inexistente}/descartar", ["AdminTarifas", "Admin"]),

            // ---- herramientas de lectura del asistente (#371): el endpoint deja pasar a todo usuario autenticado; el rol limita CADA herramienta
            new("Asistente GET herramientas", HttpMethod.Get, "/api/asistente/herramientas", Todos),
            new("Asistente POST herramienta (todos los roles)", HttpMethod.Post, "/api/asistente/herramientas/consultar_piso_sicetac", Todos, new { }),
            new("Asistente POST consultar_tarifa_plaza_nexo", HttpMethod.Post, "/api/asistente/herramientas/consultar_tarifa_plaza_nexo", Biblioteca, new { }),
            new("Asistente POST consultar_propuesta_linea", HttpMethod.Post, "/api/asistente/herramientas/consultar_propuesta_linea", Mesa, new { }),
            new("Asistente POST consultar_aprovisionamiento", HttpMethod.Post, "/api/asistente/herramientas/consultar_aprovisionamiento", ["Comercial", "Admin"], new { }),

            // ---- diccionario de términos por cliente: leer es para todos; escribir también lo hace el Comercial
            new("Diccionario cliente GET", HttpMethod.Get, "/api/diccionario-cliente", Todos),
            new("Diccionario cliente POST", HttpMethod.Post, "/api/diccionario-cliente", ["Comercial", "AdminBiblioteca", "AdminTarifas", "Admin"], new { }),
            new("Diccionario cliente PUT", HttpMethod.Put, $"/api/diccionario-cliente/{Inexistente}", ["Comercial", "AdminBiblioteca", "AdminTarifas", "Admin"], new { }),
            new("Diccionario cliente DELETE", HttpMethod.Delete, $"/api/diccionario-cliente/{Inexistente}", ["Comercial", "AdminBiblioteca", "AdminTarifas", "Admin"]),

            // ---- Cubicador completo para AdminTarifas (#310); AdminBiblioteca no
            new("planes-cargue POST", HttpMethod.Post, "/api/planes-cargue", CosteoYCubicador, new { }),
            new("planes-cargue POST /demo", HttpMethod.Post, "/api/planes-cargue/demo", CosteoYCubicador, Sondeable: false),
            new("planes-cargue POST /desde-linea", HttpMethod.Post, $"/api/planes-cargue/desde-linea/{Inexistente}", CosteoYCubicador),
            new("planes-cargue POST /optimizar", HttpMethod.Post, $"/api/planes-cargue/{Inexistente}/optimizar", CosteoYCubicador, new { }),
            new("planes-cargue POST /items", HttpMethod.Post, $"/api/planes-cargue/{Inexistente}/items", CosteoYCubicador, new { }),
            new("planes-cargue POST /items/importar", HttpMethod.Post, $"/api/planes-cargue/{Inexistente}/items/importar", CosteoYCubicador, new { }),
            new("planes-cargue DELETE /items/{id}", HttpMethod.Delete, $"/api/planes-cargue/{Inexistente}/items/{Inexistente}", CosteoYCubicador, Sondeable: false),
            new("planes-cargue DELETE /{id}", HttpMethod.Delete, $"/api/planes-cargue/{Inexistente}", CosteoYCubicador, Sondeable: false),
            new("planes-cargue PATCH /colocaciones", HttpMethod.Patch, $"/api/planes-cargue/{Inexistente}/colocaciones/{Inexistente}", CosteoYCubicador, new { }),
            new("vehiculos POST", HttpMethod.Post, "/api/vehiculos", CosteoYCubicador, new { }),
            new("vehiculos DELETE (solo Admin)", HttpMethod.Delete, $"/api/vehiculos/{Inexistente}", ["Admin"], Sondeable: false),

            // ---- cubicación rápida: AdminBiblioteca la conserva
            new("cubicacion POST", HttpMethod.Post, "/api/cubicacion", ["Comercial", "AdminBiblioteca", "AdminTarifas", "Admin"],
                new { idLineaSolicitud = "LIN-ROLES", mercancias = Array.Empty<object>() }),

            // ---- Biblioteca: AdminBiblioteca SÍ entra
            new("Tarifas Plaza GET", HttpMethod.Get, "/api/tarifas-plaza", Biblioteca),
            new("Rangos de peso GET /api/pesos", HttpMethod.Get, "/api/pesos", Biblioteca),
            new("Reglas Solo-SICETAC GET", HttpMethod.Get, "/api/reglas-solo-sicetac", Biblioteca),
            new("Vigencia plaza PATCH", HttpMethod.Patch, "/api/biblioteca-sap-plaza/999999999/vigencia", Biblioteca, new { }),
        ];
        foreach (var e in lista)
            foreach (var rol in Todos)
                yield return [e, rol];
    }

    private async Task<HttpStatusCode> Llamar(clsEndpoint e, string? rol)
    {
        var cliente = rol is null ? fx.Anon() : fx.Client(rol);
        using var peticion = new HttpRequestMessage(e.Metodo, e.Url);
        if (e.Cuerpo is not null) peticion.Content = JsonContent.Create(e.Cuerpo);
        using var respuesta = await cliente.SendAsync(peticion);
        return respuesta.StatusCode;
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Cada_rol_recibe_403_o_pasa_la_autorizacion_segun_la_lista_del_endpoint(clsEndpoint e, string rol)
    {
        if (e.Permitidos.Contains(rol))
        {
            if (!e.Sondeable) return; // pasaría y haría algo: se cubre por el lado del 403 de los demás
            var estado = await Llamar(e, rol);
            Assert.True(estado is not (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized),
                $"{e} con {rol}: debía pasar la autorización y respondió {(int)estado}.");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Forbidden, await Llamar(e, rol));
        }
    }

    [Theory]
    [MemberData(nameof(EndpointsSinSesion))]
    public async Task Sin_sesion_todos_responden_401(clsEndpoint e)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await Llamar(e, rol: null));
    }

    public static IEnumerable<object[]> EndpointsSinSesion() =>
        Endpoints().Select(o => o[0]).Distinct().Select(e => new[] { e });

    // ---- La sesión: lo que el front lee de GET /api/sesion/actual
    [Fact]
    public async Task La_sesion_de_AdminBiblioteca_trae_sus_capacidades_de_Biblioteca_y_ninguna_de_costeo()
    {
        var resp = await fx.Client("AdminBiblioteca").GetAsync("/api/sesion/actual");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var cuerpo = await resp.Json();
        Assert.Equal("AdminBiblioteca", cuerpo.GetProperty("rol").GetString());
        var capacidades = cuerpo.GetProperty("capacidades").EnumerateArray().Select(c => c.GetString()).ToList();
        Assert.Equal(["verBiblioteca", "ajustarVigencia", "editarReglasSoloSicetac"], capacidades);
        Assert.DoesNotContain("gestionarCotizacion", capacidades);
        Assert.DoesNotContain("crearSolicitud", capacidades);
    }
}
