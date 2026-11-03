using System.Net;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-24.3/#320 (métricas): una línea RECHAZADA se dejó sin costear a propósito, así que no cuenta como "por costear" en la proporción de líneas
/// autocosteadas del tablero del piloto. Sembrada en una ventana de fechas propia (año 2033, sin datos reales) para aislarse de la base compartida.
/// </summary>
[Collection("api")]
public class clsMetricasLineasRechazadasTests
{
    private readonly clsApiFixture _fx;
    public clsMetricasLineasRechazadasTests(clsApiFixture fx) => _fx = fx;

    private static readonly DateTimeOffset Creada = new(2033, 5, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Una_linea_rechazada_no_baja_el_porcentaje_de_lineas_autocosteadas()
    {
        var solicitudId = _fx.WithDb(db =>
        {
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
            };
            s.Lineas.Add(new clsLineaSolicitud { SolicitudId = s.Id, Peso = 1, EstadoLinea = EstadoLinea.AutoResuelta });
            s.Lineas.Add(new clsLineaSolicitud { SolicitudId = s.Id, Peso = 2, EstadoLinea = EstadoLinea.PorCostear });
            s.Lineas.Add(new clsLineaSolicitud { SolicitudId = s.Id, Peso = 3, EstadoLinea = EstadoLinea.Rechazada, MotivoRechazo = "Sin tarifa" });
            db.Solicitudes.Add(s);
            db.SaveChanges();
            // SaveChanges estampa CreatedAt = ahora: la fecha de la ventana se fija después, con ExecuteUpdate.
            db.LineasSolicitud.Where(l => l.SolicitudId == s.Id).ExecuteUpdate(l => l.SetProperty(x => x.CreatedAt, Creada));
            return s.Id;
        });
        try
        {
            var desde = Uri.EscapeDataString(Creada.AddDays(-1).ToString("o"));
            var hasta = Uri.EscapeDataString(Creada.AddDays(1).ToString("o"));

            var resp = await _fx.Client("LiderProceso").GetAsync($"/api/metricas/kpis-piloto?desde={desde}&hasta={hasta}");

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var j = await resp.Json();
            Assert.Equal(2, j.GetProperty("totalLineas").GetInt32());                 // la rechazada no cuenta
            Assert.Equal(1, j.GetProperty("lineasAutoCosteadas").GetInt32());
            Assert.Equal(50.0, j.GetProperty("porcentajeLineasAutoCosteadas").GetDouble());   // no 33,3
        }
        finally
        {
            try
            {
                _fx.WithDb(db =>
                {
                    db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                    db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                    return 0;
                });
            }
            catch (Microsoft.Data.SqlClient.SqlException) { }
        }
    }
}
