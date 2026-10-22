using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// Flujo de decisión del flete contra la API y el SICE-TAC reales. Como la tarifa cambia cada mes, ninguna prueba afirma un
/// valor fijo: afirman relaciones que deben cumplirse con cualquier tarifa (el flete guardado es el propuesto, el total es la
/// suma de sus partes, el tramo vacío sale de la unidad de transporte de la línea, una línea sin piso del tramo vacío queda
/// pendiente). Cada prueba crea su solicitud con una referencia marcada y la borra, con lo que haya creado, al terminar.
/// </summary>
[Collection("api")]
public class clsFlujoDeDecisionRealTests
{
    private readonly clsApiFixture _fx;
    private readonly clsDatosDePrueba _datos;

    public clsFlujoDeDecisionRealTests(clsApiFixture fx)
    {
        _fx = fx;
        _datos = new clsDatosDePrueba(fx);
        _datos.BarrerRestos();
    }

    private sealed record clsResultado(Guid SolicitudId, JsonElement Linea, JsonElement Propuesta, JsonElement? FilaCotizacion, decimal? TotalCotizacion);

    /// <summary>(id, nombre) del municipio con ese código DANE, tal como lo guarda el catálogo.</summary>
    private (int Id, string Nombre) Municipio(string codigoDane) =>
        _fx.WithDb(db => db.Municipios.Where(m => m.CodigoDane == codigoDane).Select(m => new { m.Id, m.Municipio }).AsEnumerable()
            .Select(m => (m.Id, m.Municipio)).First());

    private object Linea(string origenDane, string destinoDane, string vehiculo, string? carroceria, decimal peso, string lineaNegocio,
        string claseViaje, Action<Dictionary<string, object?>>? extra = null)
    {
        var o = Municipio(origenDane);
        var d = Municipio(destinoDane);
        var x = new Dictionary<string, object?>
        {
            ["origen"] = o.Nombre, ["destino"] = d.Nombre, ["origenMunicipioId"] = o.Id, ["destinoMunicipioId"] = d.Id,
            ["tipoVehiculo"] = vehiculo, ["carroceria"] = carroceria, ["peso"] = peso, ["lineaNegocio"] = lineaNegocio, ["claseViaje"] = claseViaje,
        };
        extra?.Invoke(x);
        return x;
    }

    /// <summary>Captura una solicitud de una línea y devuelve lo que ve la Mesa, la propuesta y la cotización auto-costeada.</summary>
    private async Task<clsResultado> Capturar(object linea, DateTimeOffset inicio)
    {
        var resp = await _fx.Client("Comercial").PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "spot", referencia = clsDatosDePrueba.NuevaReferencia(), lineas = new[] { linea },
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var solicitudId = (await resp.Json()).GetProperty("id").GetGuid();
        try
        {
            var admin = _fx.Client("AdminTarifas");
            var mesa = await admin.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}");
            var lineaMesa = mesa.GetProperty("lineas").EnumerateArray().Single();
            var propuesta = (await admin.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}/propuesta-tarifa"))
                .GetProperty("lineas").EnumerateArray().Single();

            JsonElement? fila = null;
            decimal? total = null;
            var cot = await admin.GetAsync($"/api/cotizaciones/por-solicitud/{solicitudId}");
            if (cot.StatusCode == HttpStatusCode.OK && cot.Content.Headers.ContentLength != 0)
            {
                var c = await cot.Json();
                if (c.ValueKind == JsonValueKind.Object)
                {
                    total = c.GetProperty("total").GetDecimal();
                    fila = c.GetProperty("lineas").EnumerateArray().Cast<JsonElement?>().FirstOrDefault();
                }
            }
            return new clsResultado(solicitudId, lineaMesa, propuesta, fila, total);
        }
        finally { _datos.Eliminar(solicitudId, inicio); }
    }

    private static decimal? Valor(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var p) && p.ValueKind == JsonValueKind.Object ? p.GetProperty("valor").GetDecimal() : null;

    private static string Criterio(JsonElement e, string propiedad) =>
        e.TryGetProperty(propiedad, out var p) && p.ValueKind == JsonValueKind.Object ? p.GetProperty("criterio").GetString()! : "";

    // ------------------------------------------------------------ solo SICE-TAC con levante

    [clsHechoConRutaSiceReal("5001000", "8001000", "2S2")]
    public async Task Solo_SICETAC_guarda_el_piso_y_el_aprovisionamiento_con_la_unidad_de_la_linea_y_queda_bloqueada()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        // La regla Solo-SICETAC Medellín→Barranquilla 2S2: si ya existe se respeta, si no se crea y se borra al terminar.
        var creada = await AsegurarReglaAsync("05001000", "08001000", "2S2");
        try
        {
            var r = await Capturar(Linea("05001000", "08001000", "2S2", "Furgón", 15000, "Interior a Puerto", "Extradimensionado",
                x => { x["horasCargue"] = 5; x["horasDescargue"] = 5; x["ciudadLevanteContenedor"] = "Bogotá"; }), inicio);

            Assert.Equal("SoloSicetac", r.Propuesta.GetProperty("modoDecision").GetString());
            Assert.Equal("autoResuelta", r.Linea.GetProperty("estadoLinea").GetString());
            Assert.True(r.Linea.GetProperty("tieneReglaSoloSicetac").GetBoolean());          // bloqueada para edición

            var piso = r.Linea.GetProperty("pisoSicetac");
            var esperado = piso.GetProperty("valorPiso").GetDecimal() + piso.GetProperty("valorHora").GetDecimal() * 10;
            Assert.Equal(esperado, r.FilaCotizacion!.Value.GetProperty("valorFlete").GetDecimal());   // piso + valor por hora × 5 h + 5 h

            var aprovisionamiento = Valor(r.Propuesta, "aprovisionamientoContenedor");
            Assert.NotNull(aprovisionamiento);
            Assert.Equal(aprovisionamiento, r.FilaCotizacion.Value.GetProperty("valorAprovisionamiento").GetDecimal());
            var criterio = Criterio(r.Propuesta, "aprovisionamientoContenedor");
            Assert.Contains("VACIO", criterio);
            Assert.Contains("unidad FURGON", criterio);                                      // misma carrocería que la línea
            Assert.Contains("1 h + 1 h", criterio);
            Assert.Equal(r.FilaCotizacion.Value.GetProperty("valorFlete").GetDecimal() + aprovisionamiento, r.TotalCotizacion);
        }
        finally { if (creada is { } id) await BorrarReglaAsync(id); }
    }

    // ------------------------------------------------------------ un solo candidato

    [clsHechoConRutaSiceReal("66001000", "8001000", "2S3")]
    public async Task Una_linea_con_valor_propuesto_y_sin_motivo_se_guarda_sola_con_el_valor_propuesto()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        var r = await Capturar(Linea("66001000", "08001000", "2S3", "Estacas", 10000, "Interior a Puerto", "Carga suelta",
            x => { x["horasCargue"] = 10; x["horasDescargue"] = 10; }), inicio);

        var propuesto = r.Propuesta.GetProperty("valorPropuesto");
        var sinMotivo = r.Propuesta.GetProperty("motivoPendiente").ValueKind == JsonValueKind.Null;
        Assert.Equal(propuesto.ValueKind != JsonValueKind.Null && sinMotivo, r.Linea.GetProperty("estadoLinea").GetString() == "autoResuelta");

        if (r.Linea.GetProperty("estadoLinea").GetString() == "autoResuelta")
            Assert.Equal(propuesto.GetDecimal(), r.FilaCotizacion!.Value.GetProperty("valorFlete").GetDecimal());

        // Solo piso: el proceso completo lo recomienda y se guarda (no queda pendiente por tener un único candidato).
        if (Valor(r.Propuesta, "plaza") is null && Valor(r.Propuesta, "historicoSap") is null)
        {
            Assert.Equal("ConsultaCompleta", r.Propuesta.GetProperty("modoDecision").GetString());
            Assert.Equal(Valor(r.Propuesta, "sicetac"), propuesto.GetDecimal());
            Assert.Equal("autoResuelta", r.Linea.GetProperty("estadoLinea").GetString());
        }
    }

    // ------------------------------------------------------------ devolución de contenedor

    [clsHechoConRutaSiceReal("76109000", "68001000", "2S2", "VACIO")]
    public async Task La_devolucion_de_contenedor_se_costea_con_el_viaje_vacio_y_no_tiene_retorno_ni_levante()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        var r = await Capturar(Linea("76109000", "68001000", "2S2", null, 10000, "Devolución de Contenedor", "Contenedor",
            x => { x["claseContenedor"] = "CNT20.IT"; x["consolidado"] = true; x["exclusivo"] = false; }), inicio);

        var criterio = Criterio(r.Propuesta, "sicetac");
        Assert.Contains("VACIO", criterio);                                                  // viaje vacío, no cargado
        Assert.Contains("unidad PORTACONTENEDORES", criterio);                               // sin carrocería: la unidad de contenedor
        Assert.Equal(1m, r.Linea.GetProperty("horasCargue").GetDecimal());                   // 1 h + 1 h por defecto
        Assert.Equal(1m, r.Linea.GetProperty("horasDescargue").GetDecimal());
        Assert.Null(Valor(r.Propuesta, "retornoContenedor"));
        Assert.Null(Valor(r.Propuesta, "aprovisionamientoContenedor"));

        // La plaza de una devolución solo cuenta si coincide en consolidado, exclusivo y clase de contenedor.
        if (Valor(r.Propuesta, "plaza") is not null && r.Propuesta.GetProperty("plaza").GetProperty("tablaOrigen").GetString() == "SAP")
            Assert.Contains("devolución de contenedor: consolidado, exclusivo y clase de contenedor", Criterio(r.Propuesta, "plaza"));
    }

    // ------------------------------------------------------------ retorno del contenedor

    [clsHechoConRutaSiceReal("76001000", "5615000", "2S2")]
    public async Task El_retorno_del_contenedor_sale_de_la_unidad_de_la_linea_y_se_guarda_con_el_flete()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        var r = await Capturar(Linea("76001000", "05615000", "2S2", "Furgón", 18000, "Nacional", "Carga suelta",
            x => { x["ciudadDevolucionContenedor"] = "Cali"; }), inicio);

        var retorno = Valor(r.Propuesta, "retornoContenedor");
        if (retorno is null)
        {
            // Sin piso del tramo vacío para esa ruta y unidad la línea no se guarda sola.
            Assert.Equal("porCostear", r.Linea.GetProperty("estadoLinea").GetString());
            Assert.Contains("retorno del contenedor a Cali", r.Linea.GetProperty("motivoPendiente").GetString());
            return;
        }

        var criterio = Criterio(r.Propuesta, "retornoContenedor");
        Assert.Contains("VACIO", criterio);
        Assert.Contains("unidad FURGON", criterio);
        if (r.Linea.GetProperty("estadoLinea").GetString() == "autoResuelta")
            Assert.Equal(retorno, r.FilaCotizacion!.Value.GetProperty("valorRetornoContenedor").GetDecimal());
    }

    // ------------------------------------------------------------ levante sin piso del tramo vacío

    [clsHechoConRutaSiceReal("5001000", "54001000", "2S2")]
    public async Task Un_levante_sin_piso_del_tramo_vacio_deja_la_linea_pendiente_y_no_guarda_nada()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);
        // Una ciudad de levante que no existe en ninguna ruta: nunca hay piso, sea cual sea la tarifa del mes.
        var r = await Capturar(Linea("05001000", "54001000", "2S2", "Estacas", 15000, "Nacional", "Carga suelta",
            x => { x["ciudadLevanteContenedor"] = "Zzz Ciudad Inexistente"; }), inicio);

        Assert.Equal("porCostear", r.Linea.GetProperty("estadoLinea").GetString());
        Assert.Contains("levante del contenedor desde Zzz Ciudad Inexistente", r.Linea.GetProperty("motivoPendiente").GetString());
        Assert.Null(Valor(r.Propuesta, "aprovisionamientoContenedor"));
        Assert.Null(r.FilaCotizacion);                                                       // no se guardó nada, ni el flete
    }

    // ------------------------------------------------------------ estimación del levante

    [clsHechoConRutaSiceReal("11001000", "5001000", "2S2", "VACIO")]
    public async Task La_estimacion_del_levante_con_carroceria_usa_la_unidad_y_sin_ella_avisa_que_es_aproximada()
    {
        var cli = _fx.Client("Comercial");
        const string Ruta = "/api/captura-solicitudes/aprovisionamiento?levante=Bogot%C3%A1&origen=Medell%C3%ADn&configuracion=2S2";

        var conCarroceria = (await cli.GetFromJsonAsync<JsonElement>(Ruta + "&carroceria=Furg%C3%B3n")).GetProperty("candidato");
        var sinCarroceria = (await cli.GetFromJsonAsync<JsonElement>(Ruta)).GetProperty("candidato");

        Assert.Contains("unidad FURGON", conCarroceria.GetProperty("criterio").GetString());
        Assert.Equal(JsonValueKind.Null, conCarroceria.GetProperty("advertencia").ValueKind);
        Assert.Contains("aproximado", sinCarroceria.GetProperty("advertencia").GetString());
        Assert.True(sinCarroceria.GetProperty("valor").GetDecimal() <= conCarroceria.GetProperty("valor").GetDecimal());   // la aproximación es la menor
    }

    // ------------------------------------------------------------ regla Solo-SICETAC de apoyo

    /// <summary>Crea la regla si no existe y devuelve su id; null si ya existía (no se toca ni se borra).</summary>
    private async Task<Guid?> AsegurarReglaAsync(string origenDane, string destinoDane, string vehiculo)
    {
        var resp = await _fx.Client("AdminTarifas").PostJson("/api/reglas-solo-sicetac",
            new { origen = origenDane, destino = destinoDane, tipoVehiculo = vehiculo });
        if (resp.StatusCode == HttpStatusCode.Created) return (await resp.Json()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        return null;
    }

    private async Task BorrarReglaAsync(Guid id) => await _fx.Client("AdminTarifas").DeleteAsync($"/api/reglas-solo-sicetac/{id}");
}
