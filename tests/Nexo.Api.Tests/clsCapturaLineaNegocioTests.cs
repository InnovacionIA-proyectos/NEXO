using System.Net;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Enums;

namespace Nexo.Api.Tests;

/// <summary>
/// La línea de negocio en la captura, contra la API y la base reales: una de las seis se guarda, vacía queda como hasta ahora y un texto que
/// no es ninguna de las seis se rechaza con el número de línea y el valor recibido, en lugar de guardarse en blanco sin avisar.
/// </summary>
[Collection("api")]
public class clsCapturaLineaNegocioTests
{
    private readonly clsApiFixture _fx;
    private readonly clsDatosDePrueba _datos;

    public clsCapturaLineaNegocioTests(clsApiFixture fx)
    {
        _fx = fx;
        _datos = new clsDatosDePrueba(fx);
    }

    private Task<HttpResponseMessage> Capturar(string referencia, params string?[] lineasDeNegocio) =>
        _fx.Client("Comercial").PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "spot", referencia,
            lineas = lineasDeNegocio.Select(l => new { origen = "Bogotá", destino = "Medellín", tipoVehiculo = "3S3", peso = 1000, lineaNegocio = l }).ToArray(),
        });

    [Theory]
    [InlineData("puertoAInterior", LineaNegocio.PuertoAInterior)]
    [InlineData("Interior a puerto", LineaNegocio.InteriorAPuerto)]
    [InlineData("Devolución de contenedor", LineaNegocio.DevolucionContenedor)]
    [InlineData("NACIONAL", LineaNegocio.Nacional)]
    public async Task Una_de_las_seis_lineas_se_guarda(string texto, LineaNegocio esperada)
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

        var resp = await Capturar(clsDatosDePrueba.NuevaReferencia(), texto);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var id = (await resp.Json()).GetProperty("id").GetGuid();
        try { Assert.Equal(esperada, _fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.SolicitudId == id).LineaNegocio)); }
        finally { _datos.Eliminar(id, inicio); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Una_linea_de_negocio_vacia_sigue_como_hasta_ahora(string? texto)
    {
        var inicio = DateTimeOffset.UtcNow.AddMinutes(-1);

        var resp = await Capturar(clsDatosDePrueba.NuevaReferencia(), texto);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var id = (await resp.Json()).GetProperty("id").GetGuid();
        try { Assert.Null(_fx.WithDb(db => db.LineasSolicitud.AsNoTracking().Single(l => l.SolicitudId == id).LineaNegocio)); }
        finally { _datos.Eliminar(id, inicio); }
    }

    [Theory]
    [InlineData("Impo")]
    [InlineData("Outbound")]
    public async Task Un_texto_que_no_es_una_de_las_seis_lineas_es_400_con_la_linea_y_el_valor_y_no_se_guarda_nada(string texto)
    {
        var referencia = clsDatosDePrueba.NuevaReferencia();

        var resp = await Capturar(referencia, "Nacional", texto);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("LINEA_NEGOCIO_INVALIDO", await resp.ErrorCode());
        var cuerpo = await resp.Content.ReadAsStringAsync();
        Assert.Contains("Línea 2", cuerpo);
        Assert.Contains(texto, cuerpo);
        Assert.Contains("Puerto a interior", cuerpo);
        Assert.False(_fx.WithDb(db => db.Solicitudes.Any(s => s.CondicionesServicio != null && s.CondicionesServicio.Contains(referencia))));
    }
}
