using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Biblioteca.Interfaces;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.External;

namespace Nexo.Api.Tests;

/// <summary>
/// #300 contra la API, la base y el DWSice REALES.
///
/// (2) El piso SICE-TAC solo se usa si su período es el del mes y año actuales (sin mes de gracia). El reloj de la
/// aplicación se fija con relación al período MÁS RECIENTE que de verdad tiene DWSice, para que la prueba no dependa de
/// qué mes sea hoy: con el reloj en ese mes el piso se usa; con el reloj un mes después, no, y sale el motivo.
/// (3) Tramos vacíos con 1h+1h, separados de las horas del viaje cargado.
///
/// La ruta lleva municipio resuelto (Buenaventura 1012 → Bogotá 149) y vehículo 3S3: sin eso el motor no encuentra
/// piso y la prueba no probaría nada (mismo dato que clsCapturaContratoCotiflyTests).
/// </summary>
[Collection("api")]
public class clsSicetacPeriodoYTramosVaciosTests
{
    private readonly clsApiFixture _fx;
    public clsSicetacPeriodoYTramosVaciosTests(clsApiFixture fx) => _fx = fx;

    private sealed class clsRelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    /// <summary>Período AAAAMM más reciente que de verdad tiene DWSice.</summary>
    private int PeriodoMasRecienteDelDw()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var dw = scope.ServiceProvider.GetRequiredService<clsDwSiceDbContext>();
        return dw.RutasSicetac.Where(r => r.Periodo > 0).Max(r => (int?)r.Periodo) ?? 0;
    }

    /// <summary>Mediados del mes del período AAAAMM, en UTC a mediodía (lejos de cualquier borde de huso).</summary>
    private static DateTimeOffset MedioDelMes(int periodo, int mesesDespues = 0) =>
        new DateTimeOffset(periodo / 100, periodo % 100, 15, 17, 0, 0, TimeSpan.Zero).AddMonths(mesesDespues);

    private WebApplicationFactoryConReloj ConReloj(DateTimeOffset ahora) => new(_fx, ahora);

    /// <summary>Fábrica derivada de la de pruebas con el reloj fijo; se desecha con la prueba.</summary>
    private sealed class WebApplicationFactoryConReloj : IDisposable
    {
        private readonly Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<clsProgram> _factory;
        public WebApplicationFactoryConReloj(clsApiFixture fx, DateTimeOffset ahora) =>
            _factory = fx.Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(new clsRelojFijo(ahora));
            }));
        public IServiceProvider Services => _factory.Services;
        public void Dispose() => _factory.Dispose();
    }

    /// <param name="rutaConNombreReal">El retorno del contenedor se busca por TEXTO (la ciudad de devolución no tiene municipio
    /// resuelto), así que necesita el nombre de ciudad limpio como lo escribe el comercial: "Bogotá", no "Bogotá (test300-x)".
    /// Reusa la ruta si ya existe (Origen/Destino es único) y nunca la borra.</param>
    private (Guid SolicitudId, Guid LineaId) CrearLinea(
        bool retornaContenedor = false, string? ciudadDevolucion = null, decimal? horasCargue = null, decimal? horasDescargue = null,
        bool rutaConNombreReal = false)
    {
        return _fx.WithDb(db =>
        {
            var solicitud = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
                CondicionesServicio = "{\"tiemposLogisticos\":{\"horasCargue\":4,\"horasDescargue\":4}}",
            };
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var existente = rutaConNombreReal ? db.Rutas.FirstOrDefault(r => r.Origen == "Buenaventura" && r.Destino == "Bogotá") : null;
            var ruta = existente ?? new clsRuta
            {
                Origen = rutaConNombreReal ? "Buenaventura" : $"Buenaventura (test300-{sufijo})",
                Destino = rutaConNombreReal ? "Bogotá" : $"Bogotá (test300-{sufijo})",
                OrigenMunicipioId = 1012, DestinoMunicipioId = 149,
            };
            var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = "3S3" };
            var linea = new clsLineaSolicitud
            {
                SolicitudId = solicitud.Id, clsRuta = ruta, RutaId = ruta.Id,
                clsConfiguracionVehiculo = vehiculo, ConfigVehiculoId = vehiculo.Id,
                Peso = 18000, ClaseViaje = ClaseViaje.Cupo,
                RetornaContenedor = retornaContenedor, CiudadDevolucionContenedor = ciudadDevolucion,
                HorasCargue = horasCargue, HorasDescargue = horasDescargue,
            };
            solicitud.Lineas.Add(linea);
            if (existente is null) db.Rutas.Add(ruta);
            db.ConfiguracionesVehiculo.Add(vehiculo);
            db.Solicitudes.Add(solicitud);
            db.SaveChanges();
            return (solicitud.Id, linea.Id);
        });
    }

    private void Limpiar(Guid solicitudId) => _fx.WithDb(db =>
    {
        db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
        db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
        db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
        return 0;
    });

    // ------------------------------------------------------------------ (2) SICE-TAC solo del mes actual

    [Fact]
    public async Task Con_el_reloj_en_el_periodo_del_DW_el_piso_SICETAC_se_usa()
    {
        var periodo = PeriodoMasRecienteDelDw();
        Assert.True(periodo > 0, "DWSice no trae ningún período: no se puede probar el piso SICE-TAC.");
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            using var fabrica = ConReloj(MedioDelMes(periodo));
            using var scope = fabrica.Services.CreateScope();
            var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

            var propuesta = (await propuestas.ProponerAsync(solicitudId)).Lineas.Single(l => l.LineaId == lineaId);

            Assert.NotNull(propuesta.Sicetac);
            Assert.DoesNotContain("SICE-TAC no se usa", propuesta.Justificacion);
            Assert.Null(await propuestas.MotivoSicetacNoUsadoAsync());
            Assert.NotNull(await propuestas.PisoSicetacVigenteAsync("Buenaventura", "Bogotá", "3S3", null, null));
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Con_el_reloj_un_mes_despues_del_periodo_del_DW_el_piso_no_se_usa_y_sale_el_motivo()
    {
        var periodo = PeriodoMasRecienteDelDw();
        Assert.True(periodo > 0, "DWSice no trae ningún período: no se puede probar el piso SICE-TAC.");
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            using var fabrica = ConReloj(MedioDelMes(periodo, mesesDespues: 1));
            using var scope = fabrica.Services.CreateScope();
            var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

            var propuesta = (await propuestas.ProponerAsync(solicitudId)).Lineas.Single(l => l.LineaId == lineaId);

            Assert.Null(propuesta.Sicetac);
            // Sin piso nunca hay modo automático: la línea queda para que una persona la valide.
            Assert.Equal(nameof(ModoDecision.ConsultaCompleta), propuesta.ModoDecision);
            Assert.NotNull(propuesta.MotivoPendiente);
            Assert.Contains("SICE-TAC no se usa", propuesta.MotivoPendiente);
            Assert.Contains(periodo.ToString(), propuesta.MotivoPendiente);
            Assert.Contains(clsPeriodoSicetac.Actual(MedioDelMes(periodo, 1)).ToString(), propuesta.MotivoPendiente);
            Assert.StartsWith("SICE-TAC no se usa", propuesta.Justificacion);

            // Mesa de Costeo: sin piso, y el aviso dice por qué.
            Assert.Null(await propuestas.PisoSicetacVigenteAsync("Buenaventura", "Bogotá", "3S3", null, null));
            Assert.Contains("SICE-TAC no se usa", await propuestas.MotivoSicetacNoUsadoAsync());

            // El aprovisionamiento (tramo vacío del levante) tampoco usa un SICE-TAC de otro mes.
            Assert.Null(await propuestas.EstimarAprovisionamientoAsync("Cartagena", "Bogotá", "3S3"));

            // Y lo mismo por la ruta de una sola línea, la del auto-costeo asíncrono.
            var porLinea = scope.ServiceProvider.GetRequiredService<Nexo.Application.CasosDeUso.Costeo.Interfaces.IPropuestaDeLineaPort>();
            var decision = await porLinea.ProponerParaLineaAsync(lineaId);
            Assert.Equal(ModoDecision.ConsultaCompleta, decision.Modo);
            Assert.Contains("SICE-TAC no se usa", decision.MotivoPendiente);
        }
        finally { Limpiar(solicitudId); }
    }

    // ------------------------------------------------------------------ (3) tramos vacíos con 1h+1h

    [Fact]
    public async Task El_levante_en_otra_ciudad_se_estima_como_viaje_vacio_SICETAC_con_2_horas_en_total()
    {
        var periodo = PeriodoMasRecienteDelDw();
        using var fabrica = ConReloj(MedioDelMes(periodo));
        using var scope = fabrica.Services.CreateScope();
        var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

        var candidato = await propuestas.EstimarAprovisionamientoAsync("Cartagena", "Bogotá", "3S3");

        Assert.NotNull(candidato);
        Assert.Contains("VACIO", candidato!.Criterio);
        // Filas VACIO (Sin carga) traen ValorHora 0: con 2 h el piso es el valor base, nunca el de las 8 h pactadas.
        Assert.Equal(candidato.ValorPisoBase, candidato.Valor);
    }

    [Fact]
    public async Task El_retorno_del_contenedor_es_un_viaje_vacio_con_1_mas_1_y_el_cargado_conserva_sus_horas()
    {
        var periodo = PeriodoMasRecienteDelDw();
        var (solicitudId, lineaId) = CrearLinea(retornaContenedor: true, ciudadDevolucion: "Cartagena", horasCargue: 4, horasDescargue: 4, rutaConNombreReal: true);
        try
        {
            using var fabrica = ConReloj(MedioDelMes(periodo));
            using var scope = fabrica.Services.CreateScope();
            var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

            var propuesta = (await propuestas.ProponerAsync(solicitudId)).Lineas.Single(l => l.LineaId == lineaId);

            var retorno = propuesta.RetornoContenedor;
            Assert.NotNull(retorno);
            Assert.Equal("retorno_contenedor", retorno!.Fuente);
            Assert.Contains("viaje vacío", retorno.Criterio);
            Assert.Contains("1 h + 1 h", retorno.Criterio);
            Assert.Contains("viaje vacío SICE-TAC, 1 h + 1 h", propuesta.Justificacion);

            // El viaje cargado NO cambió: su piso se calculó con las horas de la línea (4+4 = las pactadas) — el valor
            // de horas es el que trae DWSice para 8 h, no el de 2.
            Assert.NotNull(propuesta.Sicetac);
            Assert.True(propuesta.Sicetac!.ValorTiemposLogisticos > 0m);
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Marcar_retorna_contenedor_ya_no_cambia_las_horas_del_viaje_cargado()
    {
        // Misma línea, con y sin retorno: el piso del viaje CARGADO es idéntico. Antes, marcar el retorno bajaba las
        // horas de toda la línea a 2 h y el piso cambiaba de valor.
        var periodo = PeriodoMasRecienteDelDw();
        var (solicitudSin, lineaSin) = CrearLinea(horasCargue: 4, horasDescargue: 4);
        var (solicitudCon, lineaCon) = CrearLinea(retornaContenedor: true, ciudadDevolucion: "Cartagena", horasCargue: 4, horasDescargue: 4);
        try
        {
            using var fabrica = ConReloj(MedioDelMes(periodo));
            using var scope = fabrica.Services.CreateScope();
            var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaTarifaPort>();

            var sin = (await propuestas.ProponerAsync(solicitudSin)).Lineas.Single(l => l.LineaId == lineaSin);
            var con = (await propuestas.ProponerAsync(solicitudCon)).Lineas.Single(l => l.LineaId == lineaCon);

            Assert.NotNull(sin.Sicetac);
            Assert.NotNull(con.Sicetac);
            Assert.Equal(sin.Sicetac!.Valor, con.Sicetac!.Valor);
        }
        finally { Limpiar(solicitudSin); Limpiar(solicitudCon); }
    }

    // ------------------------------------------------------------------ (3) horas al capturar (API de Cotifly y Excel)

    private async Task<Guid> Capturar(params object[] lineas)
    {
        var resp = await _fx.Client().PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "licitacion", lineas,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Json()).GetProperty("id").GetGuid();
    }

    private static object LineaCaptura(
        string? lineaNegocio = null, bool? retorna = null, string? ciudadDevolucion = null,
        decimal? horasCargue = null, decimal? horasDescargue = null) => new
    {
        origen = "Buenaventura", destino = "Bogotá", tipoVehiculo = "3S3", peso = 18000,
        origenMunicipioId = 1012, destinoMunicipioId = 149,
        lineaNegocio, claseViaje = "Contenedor",
        retornaContenedor = retorna, ciudadDevolucionContenedor = ciudadDevolucion,
        horasCargue, horasDescargue,
    };

    [Fact]
    public async Task La_captura_asigna_1_mas_1_a_una_devolucion_4_mas_4_a_un_retorno_y_respeta_las_horas_explicitas()
    {
        var solicitudId = await Capturar(
            LineaCaptura(lineaNegocio: "DevolucionContenedor"),                                          // devolución sin horas  -> 1+1
            LineaCaptura(retorna: true, ciudadDevolucion: "Cartagena"),                                  // cargado que retorna    -> 4+4
            LineaCaptura(lineaNegocio: "DevolucionContenedor", horasCargue: 3, horasDescargue: 5));      // devolución con horas   -> 3+5
        try
        {
            var lineas = _fx.WithDb(db => db.LineasSolicitud.AsNoTracking()
                .Where(l => l.SolicitudId == solicitudId).OrderBy(l => l.NumeroLinea).ToList());

            Assert.Equal(3, lineas.Count);
            Assert.Equal((1m, 1m), (lineas[0].HorasCargue, lineas[0].HorasDescargue));
            Assert.Equal((4m, 4m), (lineas[1].HorasCargue, lineas[1].HorasDescargue));
            Assert.True(lineas[1].RetornaContenedor);
            Assert.Equal((3m, 5m), (lineas[2].HorasCargue, lineas[2].HorasDescargue));
        }
        finally { Limpiar(solicitudId); }
    }
}
