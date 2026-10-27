using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Biblioteca.DTOs;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests;

/// <summary>
/// El piso SICE-TAC "oficial" de Contenedor/Cupo contra DWSice REAL: con los dos municipios resueltos compara la ruta exacta por
/// código DANE. El texto "%YUMBO%-%CALI%" también coincide con "YUMBO - CALIMA" y, al tomar el valor más alto, el camino por
/// texto elegía la ciudad equivocada. El reloj se fija en el período MÁS RECIENTE que tiene DWSice para que la prueba no dependa
/// del mes en que se corra.
/// </summary>
[Collection("api")]
public class clsSicetacOficialPorDaneTests
{
    private const string DaneYumbo = "76892000", DaneCali = "76001000";
    private const string DaneCartagena = "13001000", DaneCienagaMagdalena = "47189000";

    private readonly clsApiFixture _fx;
    public clsSicetacOficialPorDaneTests(clsApiFixture fx) => _fx = fx;

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

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<clsProgram> FabricaConReloj(int periodo)
    {
        var ahora = new DateTimeOffset(periodo / 100, periodo % 100, 15, 17, 0, 0, TimeSpan.Zero);
        return _fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(new clsRelojFijo(ahora));
        }));
    }

    private async Task<clsResultadoTarifarioSap?> Consultar(
        string origen, string destino, string vehiculo, decimal peso, string? origenDane, string? destinoDane)
    {
        var periodo = PeriodoMasRecienteDelDw();
        Assert.True(periodo > 0, "DWSice no trae ningún período: no se puede probar el piso SICE-TAC.");
        using var fabrica = FabricaConReloj(periodo);
        using var scope = fabrica.Services.CreateScope();
        var tarifario = scope.ServiceProvider.GetRequiredService<ITarifarioSapPort>();

        return await tarifario.ConsultarAsync(new clsConsultaTarifarioSap(
            origen, destino, LineaNegocio: null, ClaseViaje.Cupo, ClaseContenedor: null, vehiculo, Carroceria: "FURGON",
            peso, Exclusivo: false, Consolidado: false, OrigenDane: origenDane, DestinoDane: destinoDane));
    }

    [Fact]
    public async Task Con_los_dos_municipios_resueltos_Yumbo_Cali_toma_la_ruta_de_Cali_y_no_la_de_Calima()
    {
        var r = await Consultar("YUMBO", "CALI", "2", 8000, DaneYumbo, DaneCali);

        var piso = r?.SicetacOficial;
        Assert.NotNull(piso);
        Assert.StartsWith("YUMBO VALLE DEL CAUCA - CALI VALLE DEL CAUCA", piso!.Detalle);
        Assert.Contains($"ruta DANE {DaneYumbo}-{DaneCali}", piso.Criterio);
        Assert.True(piso.Valor > 0);
    }

    [Fact]
    public async Task Con_los_dos_municipios_resueltos_Cartagena_Cienaga_toma_Cienaga_Magdalena_y_no_Cienaga_de_Oro()
    {
        var r = await Consultar("CARTAGENA", "CIENAGA", "3S3", 30000, DaneCartagena, DaneCienagaMagdalena);

        var piso = r?.SicetacOficial;
        Assert.NotNull(piso);
        Assert.Contains("CIENAGA MAGDALENA", piso!.Detalle);
        Assert.DoesNotContain("CORDOBA", piso.Detalle);
    }

    [Fact]
    public async Task Sin_codigos_DANE_el_texto_del_nombre_de_la_ruta_sigue_siendo_el_respaldo()
    {
        var r = await Consultar("YUMBO", "CALI", "2", 8000, origenDane: null, destinoDane: null);

        var piso = r?.SicetacOficial;
        Assert.NotNull(piso);
        Assert.DoesNotContain("ruta DANE", piso!.Criterio);
    }

    [Fact]
    public async Task Con_un_solo_codigo_DANE_tambien_se_busca_por_texto()
    {
        var r = await Consultar("YUMBO", "CALI", "2", 8000, DaneYumbo, destinoDane: null);

        var piso = r?.SicetacOficial;
        Assert.NotNull(piso);
        Assert.DoesNotContain("ruta DANE", piso!.Criterio);
    }

    [Fact]
    public async Task Con_codigos_DANE_que_no_tienen_ruta_no_hay_piso_aunque_el_texto_coincidiera()
    {
        var r = await Consultar("YUMBO", "CALI", "2", 8000, "99999000", "99998000");

        Assert.Null(r?.SicetacOficial);
    }

    [Fact]
    public async Task La_propuesta_de_una_linea_de_Cupo_usa_el_DANE_de_su_ruta_y_no_el_texto_de_los_nombres()
    {
        var periodo = PeriodoMasRecienteDelDw();
        // Los nombres de la ruta llevan un sufijo único: por texto no coincidirían con ninguna ruta de SICE-TAC, así que solo
        // el código DANE de sus municipios (Yumbo 1046 y Cali 1006) puede encontrar el piso.
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var (solicitudId, lineaId) = _fx.WithDb(db =>
        {
            var solicitud = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
            };
            var ruta = new clsRuta
            {
                Origen = $"Yumbo (test378-{sufijo})", Destino = $"Cali (test378-{sufijo})",
                OrigenMunicipioId = 1046, DestinoMunicipioId = 1006,
            };
            var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = "2", Carroceria = "FURGON" };
            var linea = new clsLineaSolicitud
            {
                SolicitudId = solicitud.Id, clsRuta = ruta, RutaId = ruta.Id,
                clsConfiguracionVehiculo = vehiculo, ConfigVehiculoId = vehiculo.Id,
                Peso = 8000, ClaseViaje = ClaseViaje.Cupo,
            };
            solicitud.Lineas.Add(linea);
            db.Rutas.Add(ruta);
            db.ConfiguracionesVehiculo.Add(vehiculo);
            db.Solicitudes.Add(solicitud);
            db.SaveChanges();
            return (solicitud.Id, linea.Id);
        });
        try
        {
            using var fabrica = FabricaConReloj(periodo);
            using var scope = fabrica.Services.CreateScope();
            var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

            var propuesta = (await propuestas.ProponerAsync(solicitudId)).Lineas.Single(l => l.LineaId == lineaId);

            Assert.NotNull(propuesta.Sicetac);
            Assert.StartsWith("YUMBO VALLE DEL CAUCA - CALI VALLE DEL CAUCA", propuesta.Sicetac!.Detalle);
            // "oficial": el piso salió de SicetacOficialAsync. Si no recibiera el DANE, no encontraría la ruta (el nombre lleva
            // sufijo) y el piso saldría del selector de respaldo, cuyo criterio empieza por "periodo".
            Assert.StartsWith("oficial", propuesta.Sicetac.Criterio);
            Assert.Contains($"ruta DANE {DaneYumbo}-{DaneCali}", propuesta.Sicetac.Criterio);
        }
        finally { Limpiar(solicitudId, sufijo); }
    }

    /// <summary>Limpieza de MEJOR ESFUERZO: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba cuyas
    /// aserciones ya pasaron. Borra solo lo que esta prueba creó (solicitud, línea, vehículo y la ruta de sufijo único).</summary>
    private void Limpiar(Guid solicitudId, string sufijo)
    {
        try
        {
            _fx.WithDb(db =>
            {
                var vehiculos = db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId && l.ConfigVehiculoId != null)
                    .Select(l => l.ConfigVehiculoId!.Value).ToList();
                var rutas = db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).Select(l => l.RutaId).ToList();
                db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
                db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
                db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
                db.ConfiguracionesVehiculo.Where(v => vehiculos.Contains(v.Id) && !db.LineasSolicitud.Any(l => l.ConfigVehiculoId == v.Id)).ExecuteDelete();
                db.Rutas.Where(r => rutas.Contains(r.Id) && r.Origen.Contains(sufijo) && !db.LineasSolicitud.Any(l => l.RutaId == r.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
    }
}
