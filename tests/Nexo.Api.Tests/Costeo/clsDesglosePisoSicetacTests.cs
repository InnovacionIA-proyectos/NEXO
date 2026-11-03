using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests.Costeo;

/// <summary>
/// S5-HU-20.1/#286 contra DWSice real: el candidato SICE-TAC trae el desglose del piso en
/// <c>valorPisoBase</c> + <c>valorTiemposLogisticos</c> y suman EXACTAMENTE el <c>valor</c>
/// (invariante), con las horas reales de la línea; plaza e histórico no traen esos campos.
/// Buenaventura → Bogotá es la ruta con piso SICE-TAC real que ya usan las pruebas de QA.
/// </summary>
[Collection("api")]
public class clsDesglosePisoSicetacTests
{
    private readonly clsApiFixture _fx;
    public clsDesglosePisoSicetacTests(clsApiFixture fx) => _fx = fx;

    private Guid NuevaSolicitud(int horasCargue, int horasDescargue)
    {
        return _fx.WithDb(db =>
        {
            var o = db.Municipios.First(m => m.Pais == "CO" && m.Municipio == "BUENAVENTURA");
            var d = db.Municipios.First(m => m.Pais == "CO" && m.Municipio == "BOGOTA, D.C.");
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Licitacion, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
                CondicionesServicio = $"{{\"tiemposLogisticos\":{{\"horasCargue\":{horasCargue},\"horasDescargue\":{horasDescargue}}}}}",
            };
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var ruta = new clsRuta
            {
                Origen = $"{o.Municipio} (t286-{sufijo})", Destino = $"{d.Municipio} (t286-{sufijo})",
                OrigenMunicipioId = o.Id, DestinoMunicipioId = d.Id,
            };
            var veh = new clsConfiguracionVehiculo { TipoVehiculo = "CA" };
            s.Lineas.Add(new clsLineaSolicitud
            {
                SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, clsConfiguracionVehiculo = veh, ConfigVehiculoId = veh.Id,
                Peso = 24500, LineaNegocio = LineaNegocio.PuertoAInterior, ClaseViaje = ClaseViaje.Contenedor,
            });
            db.Rutas.Add(ruta); db.ConfiguracionesVehiculo.Add(veh); db.Solicitudes.Add(s); db.SaveChanges();
            return s.Id;
        });
    }

    private async Task<JsonElement> Sicetac(Guid solicitudId)
    {
        var json = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}/propuesta-tarifa");
        var sicetac = json.GetProperty("lineas")[0].GetProperty("sicetac");
        Assert.NotEqual(JsonValueKind.Null, sicetac.ValueKind);
        return sicetac;
    }

    [Theory]
    [InlineData(4, 4)]    // horas habituales
    [InlineData(40, 40)]  // muy por encima de las pactadas: el desglose tiene que reflejarlas
    public async Task El_piso_base_mas_los_tiempos_suman_exactamente_el_valor(int cargue, int descargue)
    {
        var sicetac = await Sicetac(NuevaSolicitud(cargue, descargue));

        var valor = sicetac.GetProperty("valor").GetDecimal();
        var pisoBase = sicetac.GetProperty("valorPisoBase").GetDecimal();
        var tiempos = sicetac.GetProperty("valorTiemposLogisticos").GetDecimal();

        Assert.True(pisoBase > 0, "el piso base de la fila de DWSice debe venir");
        Assert.Equal(valor, pisoBase + tiempos);
    }

    [Fact]
    public async Task Con_muchas_mas_horas_los_tiempos_logisticos_no_bajan()
    {
        var pocas = await Sicetac(NuevaSolicitud(4, 4));
        var muchas = await Sicetac(NuevaSolicitud(40, 40));

        Assert.Equal(pocas.GetProperty("valorPisoBase").GetDecimal(), muchas.GetProperty("valorPisoBase").GetDecimal());
        Assert.True(muchas.GetProperty("valorTiemposLogisticos").GetDecimal() >= pocas.GetProperty("valorTiemposLogisticos").GetDecimal());
    }

    [Fact]
    public async Task Plaza_e_historico_no_traen_el_desglose()
    {
        var json = await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{NuevaSolicitud(4, 4)}/propuesta-tarifa");
        var linea = json.GetProperty("lineas")[0];

        foreach (var candidato in new[] { "plaza", "historicoSap" })
        {
            var c = linea.GetProperty(candidato);
            if (c.ValueKind == JsonValueKind.Null) continue;
            Assert.Equal(JsonValueKind.Null, c.GetProperty("valorPisoBase").ValueKind);
            Assert.Equal(JsonValueKind.Null, c.GetProperty("valorTiemposLogisticos").ValueKind);
        }
    }
}
