using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Api.Tests.PlanDeCargue;

/// <summary>
/// #304 contra la API y la base reales (necesita docs/Database/304_rechazo_cubicacion_por_linea.sql aplicado): el rechazo que
/// el worker del Cubicador reporta para "crear plan desde línea" queda guardado por línea y el front lo lee en
/// <c>GET /api/planes-cargue/por-linea/{lineaId}/estado</c>. Antes ese motivo solo se escribía en el log y el front sondeaba
/// para siempre.
/// </summary>
[Collection("api")]
public class clsRechazoCubicacionPorLineaTests
{
    private readonly clsApiFixture _fx;
    public clsRechazoCubicacionPorLineaTests(clsApiFixture fx) => _fx = fx;

    private (Guid SolicitudId, Guid LineaId) CrearLinea() => _fx.WithDb(db =>
    {
        var s = new clsSolicitud
        {
            ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
            CreadoPor = db.Usuarios.First().Id,
        };
        var l = new clsLineaSolicitud { SolicitudId = s.Id, Peso = 1000, Dimensiones = "[]" };
        s.Lineas.Add(l);
        db.Solicitudes.Add(s);
        db.SaveChanges();
        return (s.Id, l.Id);
    });

    private void Limpiar(Guid solicitudId)
    {
        // Mejor esfuerzo: la base de pruebas es compartida y un bloqueo ajeno no debe tumbar una prueba ya correcta.
        // El rechazo se borra solo (FK en cascada con la línea).
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

    /// <summary>Lo que hace el consumer del rechazo: registrar y guardar, en el mismo DbContext.</summary>
    private async Task<bool> Registrar(Guid lineaId, string? codigo, string? mensaje)
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var puerto = scope.ServiceProvider.GetRequiredService<IRechazoCubicacionLineaPort>();
        var existe = await puerto.RegistrarAsync(lineaId, codigo, mensaje);
        await scope.ServiceProvider.GetRequiredService<clsNexoDbContext>().SaveChangesAsync();
        return existe;
    }

    private async Task<JsonElement> Estado(Guid lineaId) =>
        await _fx.Client("Comercial").GetFromJsonAsync<JsonElement>($"/api/planes-cargue/por-linea/{lineaId}/estado");

    [Fact]
    public async Task Sin_flota_ni_rechazo_el_estado_es_procesando()
    {
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            var e = await Estado(lineaId);

            Assert.Equal("procesando", e.GetProperty("estado").GetString());
            Assert.Equal(JsonValueKind.Null, e.GetProperty("codigo").ValueKind);
            Assert.Equal(0, e.GetProperty("vehiculos").GetArrayLength());
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Un_rechazo_del_Cubicador_queda_guardado_y_el_estado_lo_muestra()
    {
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            Assert.True(await Registrar(lineaId, "FLOTA_INSUFICIENTE", "Quedó carga sin ubicar tras el último vehículo."));

            var e = await Estado(lineaId);

            Assert.Equal("rechazado", e.GetProperty("estado").GetString());
            Assert.Equal("FLOTA_INSUFICIENTE", e.GetProperty("codigo").GetString());
            Assert.Equal("Quedó carga sin ubicar tras el último vehículo.", e.GetProperty("mensaje").GetString());
            Assert.NotEqual(JsonValueKind.Null, e.GetProperty("rechazadoAt").ValueKind);
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Un_segundo_rechazo_reemplaza_al_primero_y_no_deja_dos_filas()
    {
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            await Registrar(lineaId, "VEHICULO_SIN_PRESET", "primero");
            await Registrar(lineaId, "FLOTA_INSUFICIENTE", "segundo");

            var e = await Estado(lineaId);
            Assert.Equal("FLOTA_INSUFICIENTE", e.GetProperty("codigo").GetString());
            Assert.Equal(1, _fx.WithDb(db => db.RechazosCubicacionLinea.Count(r => r.LineaSolicitudId == lineaId)));
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Un_codigo_o_mensaje_demasiado_largo_se_recorta_en_vez_de_perder_el_rechazo()
    {
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            await Registrar(lineaId, new string('C', 200), new string('M', 5000));

            var fila = _fx.WithDb(db => db.RechazosCubicacionLinea.AsNoTracking().Single(r => r.LineaSolicitudId == lineaId));
            Assert.Equal(60, fila.Codigo.Length);
            Assert.Equal(1000, fila.Mensaje.Length);
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task El_rechazo_de_un_id_que_no_es_una_linea_no_guarda_nada()
    {
        var existe = await Registrar(Guid.NewGuid(), "ERROR_INTERNO", "no hay línea");

        Assert.False(existe);
    }

    // Pedir el plan otra vez empieza de cero: el rechazo del intento anterior se borra y el estado vuelve a procesando.
    [Fact]
    public async Task Pedir_el_plan_otra_vez_borra_el_rechazo_anterior()
    {
        var (solicitudId, lineaId) = CrearLinea();
        try
        {
            await Registrar(lineaId, "FLOTA_INSUFICIENTE", "sin ubicar");
            Assert.Equal("rechazado", (await Estado(lineaId)).GetProperty("estado").GetString());

            var respuesta = await _fx.Client("Comercial").PostAsync($"/api/planes-cargue/desde-linea/{lineaId}", null);
            Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);

            Assert.Equal("procesando", (await Estado(lineaId)).GetProperty("estado").GetString());
        }
        finally { Limpiar(solicitudId); }
    }

    [Fact]
    public async Task Borrar_la_linea_borra_su_rechazo_en_cascada()
    {
        var (solicitudId, lineaId) = CrearLinea();
        await Registrar(lineaId, "FLOTA_INSUFICIENTE", "sin ubicar");

        Limpiar(solicitudId);

        Assert.False(_fx.WithDb(db => db.RechazosCubicacionLinea.Any(r => r.LineaSolicitudId == lineaId)));
    }
}
