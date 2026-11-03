using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Solicitudes;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.MapeoExcel;
using L = Nexo.Application.CasosDeUso.MapeoExcel.clsLimitesDeLineaDeCaptura;

namespace Nexo.Api.Tests;

/// <summary>
/// POST /api/captura-solicitudes/aplicar-mapeo contra la API y la base REALES: quién puede usarlo, que aplique las conversiones declaradas
/// y el diccionario del cliente, que reporte los errores por fila y que sus límites no se separen de los de la captura. No usa la IA.
/// </summary>
[Collection("api")]
public class clsAplicarMapeoApiTests(clsApiFixture fx)
{
    private const string Ruta = "/api/captura-solicitudes/aplicar-mapeo";

    private static readonly object[] Columnas =
    [
        new { indice = 0, campo = "origen", transformacion = "dividir_origen_destino" },
        new { indice = 1, campo = "peso", transformacion = "peso_toneladas_a_kg" },
        new { indice = 2, campo = "tipoVehiculo" },
        new { indice = 3, campo = "ignorar" },
    ];

    private static object Cuerpo(string[][]? filas = null, Guid? clienteId = null, object[]? columnas = null, int primeraFila = 2) => new
    {
        clienteId,
        columnas = columnas ?? Columnas,
        filas = filas ?? [["BOGOTA - CALI", "8,5", "TRACTO", "900123456-1"]],
        primeraFila,
    };

    private HttpClient ClienteCon(string rol)
    {
        var c = fx.Factory.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        return c;
    }

    // ---------------------------------------------------------------- roles

    [Fact]
    public async Task Solo_Comercial_y_Admin_aplican_un_mapeo()
    {
        foreach (var rol in new[] { "Comercial", "Admin" })
        {
            using var c = ClienteCon(rol);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
        }
        foreach (var rol in new[] { "AdminTarifas", "AdminBiblioteca", "ServiciosLogisticos", "LiderProceso" })
        {
            using var c = ClienteCon(rol);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
        }
        using var anonimo = fx.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(Ruta, Cuerpo())).StatusCode);
    }

    // ---------------------------------------------------------------- el resultado

    [Fact]
    public async Task Aplica_las_conversiones_y_devuelve_la_linea_con_la_forma_de_la_captura()
    {
        using var c = ClienteCon("Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo(primeraFila: 5));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Json();
        Assert.Equal((1, 0, 0, 0), (dto.GetProperty("totalFilas").GetInt32(), dto.GetProperty("filasVacias").GetInt32(),
            dto.GetProperty("filasConErrores").GetInt32(), dto.GetProperty("totalErrores").GetInt32()));
        var linea = Assert.Single(dto.GetProperty("lineas").EnumerateArray());
        Assert.Equal((5, "BOGOTA", "CALI", 8500m, "TRACTO"),
            (linea.GetProperty("fila").GetInt32(), linea.GetProperty("origen").GetString(), linea.GetProperty("destino").GetString(),
             linea.GetProperty("peso").GetDecimal(), linea.GetProperty("tipoVehiculo").GetString()));
        Assert.Equal(JsonValueKind.Null, linea.GetProperty("carroceria").ValueKind);
        Assert.Empty(dto.GetProperty("errores").EnumerateArray());
    }

    [Fact]
    public async Task Los_errores_por_fila_llevan_el_numero_de_fila_del_archivo_el_campo_y_un_codigo()
    {
        using var c = ClienteCon("Comercial");

        var resp = await c.PostAsJsonAsync(Ruta, Cuerpo(filas:
        [
            ["BOGOTA - CALI", "8,5", "TRACTO", ""],
            ["SOLO UNA CIUDAD", "mucho", "TRACTO", ""],
            ["", "", "", ""],
            ["MEDELLIN / PASTO", "4", "SENCILLO", ""],
        ], primeraFila: 2));

        var dto = await resp.Json();
        Assert.Equal((4, 1, 1, 2), (dto.GetProperty("totalFilas").GetInt32(), dto.GetProperty("filasVacias").GetInt32(),
            dto.GetProperty("filasConErrores").GetInt32(), dto.GetProperty("totalErrores").GetInt32()));
        Assert.Equal([2, 5], dto.GetProperty("lineas").EnumerateArray().Select(l => l.GetProperty("fila").GetInt32()));
        var errores = dto.GetProperty("errores").EnumerateArray().ToList();
        Assert.Equal([(3, "origen", "ORIGEN_DESTINO_NO_SEPARABLE"), (3, "peso", "NUMERO_INVALIDO")],
            errores.Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("campo").GetString()!, e.GetProperty("codigo").GetString()!)));
    }

    [Fact]
    public async Task El_diccionario_del_cliente_traduce_los_valores_y_no_se_filtra_a_otros()
    {
        var marca = $"T360{Guid.NewGuid():N}"[..14];
        var (cliente, otro) = fx.WithDb(db => { var ids = db.Clientes.Select(x => x.Id).Take(2).ToList(); return (ids[0], ids.Count > 1 ? ids[1] : Guid.NewGuid()); });
        using var admin = ClienteCon("Admin");
        try
        {
            var creado = await admin.PostAsJsonAsync("/api/diccionario-cliente",
                new { clienteId = cliente, termino = $"{marca} FRIGO", tipo = "valor", concepto = "REFRIGERADO", contexto = "tipoVehiculo" });
            Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
            var filas = new[] { new[] { "A - B", "8", $"{marca} frigo", "" } };

            var delCliente = await (await admin.PostAsJsonAsync(Ruta, Cuerpo(filas, cliente))).Json();
            var sinCliente = await (await admin.PostAsJsonAsync(Ruta, Cuerpo(filas))).Json();

            Assert.Equal("REFRIGERADO", delCliente.GetProperty("lineas")[0].GetProperty("tipoVehiculo").GetString());
            Assert.Equal($"{marca} frigo", sinCliente.GetProperty("lineas")[0].GetProperty("tipoVehiculo").GetString());
            Assert.NotEqual(Guid.Empty, otro);
        }
        finally { fx.WithDb(db => db.TerminosCliente.Where(t => t.Termino.StartsWith(marca)).ExecuteDelete()); }
    }

    // ---------------------------------------------------------------- entrada

    [Fact]
    public async Task Un_mapeo_invalido_es_400_con_su_codigo()
    {
        using var c = ClienteCon("Comercial");

        async Task Esperar(object cuerpo, string codigo)
        {
            var resp = await c.PostAsJsonAsync(Ruta, cuerpo);
            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal(codigo, await resp.ErrorCode());
        }

        await Esperar(Cuerpo(columnas: [new { indice = 0, campo = "origen" }, new { indice = 1, campo = "peso" }]), "CAMPO_REQUERIDO");
        await Esperar(Cuerpo(columnas: [new { indice = 0, campo = "origen" }, new { indice = 1, campo = "origen" }, new { indice = 2, campo = "destino" }]), "CAMPO_DUPLICADO");
        await Esperar(Cuerpo(columnas: [new { indice = 0, campo = "inventado" }, new { indice = 1, campo = "destino" }]), "CAMPO_INVALIDO");
        await Esperar(Cuerpo(columnas: [new { indice = 0, campo = "origen", transformacion = "peso_toneladas_a_kg" }, new { indice = 1, campo = "destino" }]), "TRANSFORMACION_NO_APLICA");
        await Esperar(Cuerpo(columnas: [new { indice = 0, campo = "origen", transformacion = "inventada" }, new { indice = 1, campo = "destino" }]), "TRANSFORMACION_INVALIDA");
    }

    [Fact]
    public async Task Un_cuerpo_sin_columnas_o_con_demasiadas_filas_es_400()
    {
        using var c = ClienteCon("Comercial");

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { filas = new[] { new[] { "x" } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { columnas = Array.Empty<object>(), filas = Array.Empty<string[]>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, new { columnas = Columnas, filas = Enumerable.Range(0, 5001).Select(_ => new[] { "A - B", "1" }).ToArray() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Ruta, Cuerpo(primeraFila: 0))).StatusCode);
    }

    // ---------------------------------------------------------------- los límites no se separan de la captura

    private static int Largo(Type tipo, string propiedad) =>
        tipo.GetProperty(propiedad)!.GetCustomAttribute<MaxLengthAttribute>()!.Length;

    private static (decimal Min, decimal Max) Rango(Type tipo, string propiedad)
    {
        var r = tipo.GetProperty(propiedad)!.GetCustomAttribute<RangeAttribute>()!;
        return (Convert.ToDecimal(r.Minimum, CultureInfo.InvariantCulture), Convert.ToDecimal(r.Maximum, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Los_limites_de_texto_son_los_del_cuerpo_de_la_captura()
    {
        var linea = typeof(clsCapturaLineaRequest);
        Assert.Equal(L.MaxCiudad, Largo(linea, nameof(clsCapturaLineaRequest.Origen)));
        Assert.Equal(L.MaxCiudad, Largo(linea, nameof(clsCapturaLineaRequest.Destino)));
        Assert.Equal(L.MaxCiudad, Largo(linea, nameof(clsCapturaLineaRequest.CiudadDevolucionContenedor)));
        Assert.Equal(L.MaxCiudad, Largo(linea, nameof(clsCapturaLineaRequest.CiudadLevanteContenedor)));
        Assert.Equal(L.MaxTipoVehiculo, Largo(linea, nameof(clsCapturaLineaRequest.TipoVehiculo)));
        Assert.Equal(L.MaxCarroceria, Largo(linea, nameof(clsCapturaLineaRequest.Carroceria)));
        Assert.Equal(L.MaxLineaNegocio, Largo(linea, nameof(clsCapturaLineaRequest.LineaNegocio)));
        Assert.Equal(L.MaxClaseViaje, Largo(linea, nameof(clsCapturaLineaRequest.ClaseViaje)));
        Assert.Equal(L.MaxClaseContenedor, Largo(linea, nameof(clsCapturaLineaRequest.ClaseContenedor)));

        var posicion = typeof(clsCapturaPosicionRequest);
        Assert.Equal(L.MaxDescripcionPosicion, Largo(posicion, nameof(clsCapturaPosicionRequest.Descripcion)));
        Assert.Equal(L.MaxCodigoTipoMercancia, Largo(posicion, nameof(clsCapturaPosicionRequest.CodigoTipoMercancia)));
    }

    [Fact]
    public void Los_rangos_numericos_son_los_del_cuerpo_de_la_captura()
    {
        var linea = typeof(clsCapturaLineaRequest);
        Assert.Equal((0m, L.MaxPeso), Rango(linea, nameof(clsCapturaLineaRequest.Peso)));
        Assert.Equal((0m, L.MaxValor), Rango(linea, nameof(clsCapturaLineaRequest.ValorMercancia)));
        Assert.Equal((0m, L.MaxHoras), Rango(linea, nameof(clsCapturaLineaRequest.HorasCargue)));
        Assert.Equal((0m, L.MaxHoras), Rango(linea, nameof(clsCapturaLineaRequest.HorasDescargue)));

        var posicion = typeof(clsCapturaPosicionRequest);
        Assert.Equal((L.MinCantidad, L.MaxCantidad), Rango(posicion, nameof(clsCapturaPosicionRequest.Cantidad)));
        foreach (var dimension in new[] { nameof(clsCapturaPosicionRequest.LargoCm), nameof(clsCapturaPosicionRequest.AnchoCm), nameof(clsCapturaPosicionRequest.AltoCm) })
            Assert.Equal((L.MinDimensionCm, L.MaxDimensionCm), Rango(posicion, dimension));
        Assert.Equal((L.MinPesoPosicionKg, L.MaxPesoPosicionKg), Rango(posicion, nameof(clsCapturaPosicionRequest.PesoKg)));
    }
}
