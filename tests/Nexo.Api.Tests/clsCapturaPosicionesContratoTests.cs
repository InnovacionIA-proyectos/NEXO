using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Common.Errors;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// #304 contra la API real: contrato de las posiciones de una línea de captura. <c>pesoKg</c> (de UNA unidad) es obligatorio,
/// y las unidades se topan en 10.000 — lo mismo que el Cubicador. Antes un peso ausente o una cantidad de 100.000 se
/// capturaban y se guardaban, y el rechazo llegaba después, lejos de donde nació el error.
/// </summary>
[Collection("api")]
public class clsCapturaPosicionesContratoTests
{
    private readonly clsApiFixture _fx;
    public clsCapturaPosicionesContratoTests(clsApiFixture fx) => _fx = fx;

    private static object Posicion(int cantidad = 5, decimal? pesoKg = 20m) => new
    {
        descripcion = "Cajas", cantidad, largoCm = 40, anchoCm = 30, altoCm = 30, pesoKg,
        codigoTipoMercancia = "CAJAS_CARTON",
    };

    private async Task<HttpResponseMessage> Capturar(params object[] posiciones) =>
        await _fx.Client().PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "spot",
            lineas = new[] { new { origen = "Bogotá", destino = "Medellín", tipoVehiculo = "3S3", posiciones } },
        });

    [Fact]
    public async Task Una_posicion_sin_pesoKg_se_rechaza_con_400()
    {
        var resp = await Capturar(new { descripcion = "Cajas", cantidad = 5, largoCm = 40, anchoCm = 30, altoCm = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Contains("pesoKg", await resp.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Una_posicion_con_pesoKg_cero_se_rechaza_con_400()
    {
        // El cero cuenta como no declarado (igual que en el Cubicador).
        var resp = await Capturar(Posicion(pesoKg: 0m));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Una_posicion_de_mas_de_10000_unidades_se_rechaza_con_400()
    {
        var resp = await Capturar(Posicion(cantidad: 10_001));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Varias_posiciones_que_suman_mas_de_10000_unidades_se_rechazan_con_el_codigo_del_Cubicador()
    {
        var resp = await Capturar(Posicion(cantidad: 6000), Posicion(cantidad: 4001));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var error = await resp.Content.ReadFromJsonAsync<clsApiError>();
        Assert.Equal("CUBICACION_DEMASIADO_GRANDE", error!.Code);
    }

    [Fact]
    public async Task Una_linea_con_el_peso_por_unidad_y_dentro_del_tope_se_captura()
    {
        var resp = await Capturar(Posicion(cantidad: 5, pesoKg: 20m));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var id = (await resp.Json()).GetProperty("id").GetGuid();
        try
        {
            // El peso de la línea es la SUMA cantidad × pesoKg por unidad: 5 × 20 = 100 kg (no 20, no 2.000).
            var peso = _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.SolicitudId == id).Peso);
            Assert.Equal(100m, peso);
        }
        finally
        {
            // Limpieza de mejor esfuerzo: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba ya correcta.
            try
            {
                _fx.WithDb(db =>
                {
                    db.Cotizaciones.Where(c => c.SolicitudId == id).ExecuteDelete();
                    db.LineasSolicitud.Where(l => l.SolicitudId == id).ExecuteDelete();
                    db.Solicitudes.Where(s => s.Id == id).ExecuteDelete();
                    return 0;
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException) { }
        }
    }
}
