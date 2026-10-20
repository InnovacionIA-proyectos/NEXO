using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests;

/// <summary>
/// S6-HU-26.1/#30 contra la API, la base y el DWSice REALES: editar el flete de una línea de CUALQUIER fuente (queda Manual),
/// salvo si la ruta y el vehículo tienen regla Solo-SICE (409 FLETE_SOLO_SICETAC); nunca bajo el piso; y si el piso no se puede
/// verificar, no se guarda sin aviso (409 PISO_SICETAC_NO_VERIFICABLE, se confirma con confirmarSinPiso).
///
/// Para que el piso SICE-TAC exista sin depender del mes en que corra la prueba, el reloj de la aplicación se fija en el período
/// MÁS RECIENTE que de verdad tiene DWSice (mismo criterio que clsSicetacPeriodoYTramosVaciosTests). La regla Solo-SICE es una
/// regla REAL ya cargada (no se escribe en tbl_reglas_solo_sicetac: sus claves van en caché).
/// </summary>
[Collection("api")]
public class clsEditarFleteSoloSicetacTests
{
    private readonly clsApiFixture _fx;
    public clsEditarFleteSoloSicetacTests(clsApiFixture fx) => _fx = fx;

    private sealed class clsRelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private int PeriodoMasRecienteDelDw()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var dw = scope.ServiceProvider.GetRequiredService<clsDwSiceDbContext>();
        return dw.RutasSicetac.Where(r => r.Periodo > 0).Max(r => (int?)r.Periodo) ?? 0;
    }

    /// <summary>Cliente HTTP (rol AdminTarifas) de una fábrica derivada con el reloj en mediados del período del DW.</summary>
    private sealed class clsClienteConReloj : IDisposable
    {
        private readonly WebApplicationFactory<clsProgram> _factory;
        public HttpClient Http { get; }

        public clsClienteConReloj(clsApiFixture fx, int periodo, string rol = "AdminTarifas")
        {
            var ahora = new DateTimeOffset(periodo / 100, periodo % 100, 15, 17, 0, 0, TimeSpan.Zero);
            _factory = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(new clsRelojFijo(ahora));
            }));
            Http = _factory.CreateClient();
            Http.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, rol);
        }

        public void Dispose() { Http.Dispose(); _factory.Dispose(); }
    }

    /// <summary>Una solicitud con una línea Cupo cuya ruta lleva municipio resuelto (DANE) y el vehículo dado.</summary>
    private (Guid SolicitudId, Guid LineaId) CrearLinea(int origenId, int destinoId, string origen, string destino, string vehiculo) =>
        _fx.WithDb(db =>
        {
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
                CondicionesServicio = "{\"tiemposLogisticos\":{\"horasCargue\":4,\"horasDescargue\":4}}",
            };
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var ruta = new clsRuta
            {
                Origen = $"{origen} (test30-{sufijo})", Destino = $"{destino} (test30-{sufijo})",
                OrigenMunicipioId = origenId, DestinoMunicipioId = destinoId,
            };
            var v = new clsConfiguracionVehiculo { TipoVehiculo = vehiculo };
            var l = new clsLineaSolicitud
            {
                SolicitudId = s.Id, clsRuta = ruta, RutaId = ruta.Id, clsConfiguracionVehiculo = v, ConfigVehiculoId = v.Id,
                Peso = 18000, ClaseViaje = ClaseViaje.Cupo,
            };
            s.Lineas.Add(l);
            db.Rutas.Add(ruta);
            db.ConfiguracionesVehiculo.Add(v);
            db.Solicitudes.Add(s);
            db.SaveChanges();
            return (s.Id, l.Id);
        });

    /// <summary>La primera regla Solo-SICE REAL cuyos dos códigos DANE resuelven a municipios de Colombia.</summary>
    private (int OrigenId, int DestinoId, string Origen, string Destino, string Vehiculo) UnaReglaReal() => _fx.WithDb(db =>
    {
        var r = db.ReglasSoloSicetac
            .Join(db.Municipios.Where(m => m.Pais == "CO"), x => x.Origen, mo => mo.CodigoDane, (x, mo) => new { x, mo })
            .Join(db.Municipios.Where(m => m.Pais == "CO"), y => y.x.Destino, md => md.CodigoDane, (y, md) => new { y.x, O = y.mo, D = md })
            .OrderBy(z => z.x.Id).First();
        return (r.O.Id, r.D.Id, r.O.Municipio, r.D.Municipio, r.x.TipoVehiculo);
    });

    private void Limpiar(Guid solicitudId)
    {
        // Mejor esfuerzo: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba ya correcta.
        try
        {
            _fx.WithDb(db =>
            {
                db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }

    /// <summary>Cotización borrador con un flete de la fuente dada. 900 M: por encima de cualquier piso.</summary>
    private static async Task<Guid> CotizacionConFlete(HttpClient cli, Guid solicitudId, Guid lineaId, string fuente)
    {
        var resp = await cli.PostJson("/api/cotizaciones", new { solicitudId });
        Assert.True(resp.IsSuccessStatusCode, "No se pudo crear la cotización.");
        var cid = (await resp.Json()).GetProperty("id").GetGuid();
        var agregar = await cli.PostJson($"/api/cotizaciones/{cid}/lineas", new { lineaSolicitudId = lineaId, valorFlete = 900_000_000, fuente });
        Assert.Equal(HttpStatusCode.OK, agregar.StatusCode);
        return cid;
    }

    // ------------------------------------------------------------------ con regla Solo-SICE

    [Fact]
    public async Task Con_regla_Solo_SICE_el_PUT_responde_409_FLETE_SOLO_SICETAC_y_el_DTO_trae_tieneReglaSoloSicetac_true()
    {
        var periodo = PeriodoMasRecienteDelDw();
        var regla = UnaReglaReal();
        var (solicitudId, lineaId) = CrearLinea(regla.OrigenId, regla.DestinoId, regla.Origen, regla.Destino, regla.Vehiculo);
        try
        {
            using var c = new clsClienteConReloj(_fx, periodo);
            var cid = await CotizacionConFlete(c.Http, solicitudId, lineaId, "tarifario");

            var resp = await c.Http.PutJson($"/api/cotizaciones/{cid}/lineas/{lineaId}", new { valorFlete = 950_000_000, confirmarSinPiso = true });

            Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
            Assert.Equal("FLETE_SOLO_SICETAC", await resp.ErrorCode());

            // La Mesa lo sabe: no debe ofrecer "Editar flete".
            var linea = (await c.Http.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}")).GetProperty("lineas")[0];
            Assert.True(linea.GetProperty("tieneReglaSoloSicetac").GetBoolean());

            // Y el valor no se tocó.
            var dto = await (await c.Http.GetAsync($"/api/cotizaciones/{cid}")).Json();
            Assert.Equal(900_000_000m, dto.GetProperty("lineas")[0].GetProperty("valorFlete").GetDecimal());
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ sin regla, con piso real

    private const int Buenaventura = 1012;
    private const int Bogota = 149;

    [Fact]
    public async Task Sin_regla_y_sobre_el_piso_un_flete_de_plaza_se_edita_y_queda_Manual()
    {
        var periodo = PeriodoMasRecienteDelDw();
        var (solicitudId, lineaId) = CrearLinea(Buenaventura, Bogota, "Buenaventura", "Bogotá", "3S3");
        try
        {
            using var c = new clsClienteConReloj(_fx, periodo);
            var cid = await CotizacionConFlete(c.Http, solicitudId, lineaId, "tarifario");

            var linea = (await c.Http.GetFromJsonAsync<JsonElement>($"/api/solicitudes/{solicitudId}")).GetProperty("lineas")[0];
            Assert.False(linea.GetProperty("tieneReglaSoloSicetac").GetBoolean());

            var resp = await c.Http.PutJson($"/api/cotizaciones/{cid}/lineas/{lineaId}", new { valorFlete = 800_000_000 });

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var fila = (await resp.Json()).GetProperty("lineas")[0];
            Assert.Equal(800_000_000m, fila.GetProperty("valorFlete").GetDecimal());
            Assert.Equal("Manual", fila.GetProperty("fuente").GetString());
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Bajo_el_piso_responde_400_FLETE_BAJO_PISO_SICETAC_y_ni_confirmando_se_guarda()
    {
        var periodo = PeriodoMasRecienteDelDw();
        var (solicitudId, lineaId) = CrearLinea(Buenaventura, Bogota, "Buenaventura", "Bogotá", "3S3");
        try
        {
            using var c = new clsClienteConReloj(_fx, periodo);
            var cid = await CotizacionConFlete(c.Http, solicitudId, lineaId, "tarifario");

            var resp = await c.Http.PutJson($"/api/cotizaciones/{cid}/lineas/{lineaId}", new { valorFlete = 1, confirmarSinPiso = true });

            Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
            Assert.Equal("FLETE_BAJO_PISO_SICETAC", await resp.ErrorCode());
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ piso no verificable (período del mes actual ausente)

    [Fact]
    public async Task Con_el_reloj_un_mes_despues_del_DW_no_hay_piso_verificable_y_el_PUT_pide_confirmacion()
    {
        var periodo = PeriodoMasRecienteDelDw();
        var (solicitudId, lineaId) = CrearLinea(Buenaventura, Bogota, "Buenaventura", "Bogotá", "3S3");
        try
        {
            string cid;
            using (var vigente = new clsClienteConReloj(_fx, periodo))
                cid = (await CotizacionConFlete(vigente.Http, solicitudId, lineaId, "tarifario")).ToString();

            // Un mes después del último período del DW: SICE-TAC no se usa (#300) y por lo tanto no hay piso que verificar.
            var siguiente = new DateTime(periodo / 100, periodo % 100, 1).AddMonths(1);
            using var c = new clsClienteConReloj(_fx, siguiente.Year * 100 + siguiente.Month);

            var sinConfirmar = await c.Http.PutJson($"/api/cotizaciones/{cid}/lineas/{lineaId}", new { valorFlete = 5 });
            Assert.Equal(HttpStatusCode.Conflict, sinConfirmar.StatusCode);
            Assert.Equal("PISO_SICETAC_NO_VERIFICABLE", await sinConfirmar.ErrorCode());

            var confirmado = await c.Http.PutJson($"/api/cotizaciones/{cid}/lineas/{lineaId}", new { valorFlete = 5, confirmarSinPiso = true });
            Assert.Equal(HttpStatusCode.OK, confirmado.StatusCode);
            Assert.Equal("Manual", (await confirmado.Json()).GetProperty("lineas")[0].GetProperty("fuente").GetString());
        }
        finally { Limpiar(solicitudId); }
    }
}
