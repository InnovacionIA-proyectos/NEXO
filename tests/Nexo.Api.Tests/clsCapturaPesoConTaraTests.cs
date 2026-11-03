using System.Net;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;

namespace Nexo.Api.Tests;

/// <summary>
/// Peso con tara en la captura, contra la API y la base reales: el peso total de la línea incluye la tara y puede ser mayor que
/// la suma de las posiciones; solo se rechaza cuando las posiciones suman más que el peso total. Cada prueba crea su solicitud con
/// una referencia marcada y la borra al terminar.
/// </summary>
[Collection("api")]
public class clsCapturaPesoConTaraTests
{
    private readonly clsApiFixture _fx;
    private readonly clsDatosDePrueba _datos;

    public clsCapturaPesoConTaraTests(clsApiFixture fx)
    {
        _fx = fx;
        _datos = new clsDatosDePrueba(fx);
    }

    private static object Posicion(int cantidad, decimal pesoKg) => new
    {
        descripcion = "Cajas", cantidad, largoCm = 100, anchoCm = 100, altoCm = 100, pesoKg, codigoTipoMercancia = "CAJAS_CARTON",
    };

    private Task<HttpResponseMessage> Capturar(decimal? peso, params object[] posiciones) =>
        CapturarConReferencia(clsDatosDePrueba.NuevaReferencia(), peso, posiciones);

    private Task<HttpResponseMessage> CapturarConReferencia(string referencia, decimal? peso, params object[] posiciones) =>
        _fx.Client("Comercial").PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "spot", referencia,
            lineas = new[] { new { origen = "Bogotá", destino = "Medellín", tipoVehiculo = "3S3", peso, posiciones } },
        });

    [Fact]
    public async Task Un_peso_total_con_tara_mayor_que_la_suma_de_las_posiciones_se_crea_con_201_y_guarda_el_peso_total()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

        var resp = await Capturar(1000m, Posicion(cantidad: 1, pesoKg: 100m));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var id = (await resp.Json()).GetProperty("id").GetGuid();
        try
        {
            var peso = _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.SolicitudId == id).Peso);
            Assert.Equal(1000m, peso);   // la tara no se suma ni se resta: se guarda lo que escribió quien captura
        }
        finally { _datos.Eliminar(id, inicio); }
    }

    [Fact]
    public async Task Unas_posiciones_que_suman_mas_que_el_peso_total_se_rechazan_con_400_y_no_se_guarda_nada()
    {
        var referencia = clsDatosDePrueba.NuevaReferencia();

        var resp = await CapturarConReferencia(referencia, 1000m, Posicion(cantidad: 12, pesoKg: 100m));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("PESO_INCONSISTENTE", await resp.ErrorCode());
        Assert.False(_fx.WithDb(db => db.Solicitudes.Any(s => s.CondicionesServicio != null && s.CondicionesServicio.Contains(referencia))));
    }

    [Fact]
    public async Task Una_linea_sin_posiciones_sigue_capturandose_solo_con_su_peso_total()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

        var resp = await Capturar(1000m);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        _datos.Eliminar((await resp.Json()).GetProperty("id").GetGuid(), inicio);
    }

    [Fact]
    public async Task El_peso_total_igual_a_la_suma_de_las_posiciones_sigue_siendo_valido()
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

        var resp = await Capturar(500m, Posicion(cantidad: 5, pesoKg: 100m));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        _datos.Eliminar((await resp.Json()).GetProperty("id").GetGuid(), inicio);
    }
}
