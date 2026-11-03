using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-20.5/#297 contra la API y la base reales: (1) el GET de la solicitud devuelve <c>numeroLinea</c> y
/// ordena las líneas de forma estable (por el orden del arreglo con que se capturaron; las líneas viejas,
/// sin número, al final); (2) una línea de Cupo sin vehículo no se autocostea.
/// Las rutas llevan municipio resuelto (Buenaventura 1012 → Bogotá 149) para que el motor SÍ encuentre piso
/// SICE-TAC y plaza: sin esos datos todo caería a consulta completa y la prueba no probaría nada.
/// </summary>
[Collection("api")]
public class clsCapturaContratoCotiflyTests
{
    private const string MotivoSinVehiculo = "Línea sin vehículo: requiere consulta completa";

    private readonly clsApiFixture _fx;
    public clsCapturaContratoCotiflyTests(clsApiFixture fx) => _fx = fx;

    private static object Linea(string origen, string destino, string? clase = null, string? vehiculo = null,
        decimal peso = 18000, Guid? grupo = null, int? origenId = 1012, int? destinoId = 149) => new
    {
        origen, destino, claseViaje = clase, tipoVehiculo = vehiculo, peso,
        grupoRutaId = grupo, origenMunicipioId = origenId, destinoMunicipioId = destinoId,
    };

    private async Task<Guid> Capturar(params object[] lineas)
    {
        var resp = await _fx.Client().PostJson("/api/captura-solicitudes", new
        {
            clienteId = _fx.AnyClienteId(), tipo = "licitacion", lineas,
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Json()).GetProperty("id").GetGuid();
    }

    private void Limpiar(Guid solicitudId, string? prefijoVehiculo = null) => _fx.WithDb(db =>
    {
        db.Cotizaciones.Where(c => c.SolicitudId == solicitudId).ExecuteDelete();
        db.LineasSolicitud.Where(l => l.SolicitudId == solicitudId).ExecuteDelete();
        db.Solicitudes.Where(s => s.Id == solicitudId).ExecuteDelete();
        if (prefijoVehiculo is not null)
            db.ConfiguracionesVehiculo.Where(v => v.TipoVehiculo.StartsWith(prefijoVehiculo)).ExecuteDelete();
        return 0;
    });

    private async Task<JsonElement> Costeo(Guid id) => await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{id}");

    // ------------------------------------------------------------------ Parte 1: numeroLinea y orden

    [Fact]
    public async Task Las_lineas_viejas_sin_numeroLinea_van_al_final_y_entre_ellas_por_fecha_de_creacion_y_luego_por_id()
    {
        var (solicitud, esperado) = _fx.WithDb(db =>
        {
            var s = new clsSolicitud
            {
                ClienteId = db.Clientes.First().Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
            };
            // Insertadas a propósito en desorden: 3, sin número (reciente), 1, sin número (vieja), 2.
            var l3 = new clsLineaSolicitud { SolicitudId = s.Id, NumeroLinea = 3, Peso = 1 };
            var nulaReciente = new clsLineaSolicitud { SolicitudId = s.Id, NumeroLinea = null, Peso = 2 };
            var l1 = new clsLineaSolicitud { SolicitudId = s.Id, NumeroLinea = 1, Peso = 3 };
            var nulaVieja = new clsLineaSolicitud { SolicitudId = s.Id, NumeroLinea = null, Peso = 4 };
            var l2 = new clsLineaSolicitud { SolicitudId = s.Id, NumeroLinea = 2, Peso = 5 };
            foreach (var l in new[] { l3, nulaReciente, l1, nulaVieja, l2 }) s.Lineas.Add(l);
            db.Solicitudes.Add(s);
            db.SaveChanges();
            // SaveChanges estampa CreatedAt = ahora: la antigüedad se fija después.
            var ahora = DateTimeOffset.UtcNow;
            db.LineasSolicitud.Where(l => l.Id == nulaVieja.Id).ExecuteUpdate(l => l.SetProperty(x => x.CreatedAt, ahora.AddDays(-30)));
            db.LineasSolicitud.Where(l => l.Id == nulaReciente.Id).ExecuteUpdate(l => l.SetProperty(x => x.CreatedAt, ahora.AddDays(-1)));
            return (s.Id, new[] { l1.Id, l2.Id, l3.Id, nulaVieja.Id, nulaReciente.Id });
        });
        try
        {
            var lineas = (await Costeo(solicitud)).GetProperty("lineas").EnumerateArray().ToList();

            Assert.Equal(esperado, lineas.Select(l => l.GetProperty("lineaId").GetGuid()));
            Assert.Equal(new int?[] { 1, 2, 3, null, null },
                lineas.Select(l => l.GetProperty("numeroLinea").ValueKind == JsonValueKind.Null ? (int?)null : l.GetProperty("numeroLinea").GetInt32()));
        }
        finally { Limpiar(solicitud); }
    }

    // ------------------------------------------------------------------ Parte 2: Cupo sin vehículo

    private (EstadoLinea Estado, string? Motivo, bool ConFlete) Estado(Guid solicitudId, int numeroLinea) => _fx.WithDb(db =>
    {
        var l = db.LineasSolicitud.AsNoTracking().Single(x => x.SolicitudId == solicitudId && x.NumeroLinea == numeroLinea);
        var conFlete = db.CotizacionLineas.Any(cl => cl.LineaSolicitudId == l.Id);
        return (l.EstadoLinea, l.MotivoPendiente, conFlete);
    });

    [Fact]
    public async Task Pliego_mixto_solo_las_de_Cupo_sin_vehiculo_o_sin_clase_quedan_pendientes_con_su_motivo()
    {
        var vehiculo = $"T297-{Guid.NewGuid():N}"[..14];
        var grupo = Guid.NewGuid();   // camiones de la misma ruta: 2, 3 y 4
        var id = await Capturar(
            Linea("Buenaventura", "Bogotá", clase: "Cupo"),                          // 1: Cupo sin vehículo        -> pendiente + motivo
            Linea("Buenaventura", "Bogotá", clase: "Cupo", vehiculo: vehiculo, grupo: grupo),   // 2: Cupo CON vehículo -> sin cambios
            Linea("Buenaventura", "Bogotá", clase: "Contenedor", grupo: grupo),     // 3: Contenedor sin vehículo -> sin cambios
            Linea("Buenaventura", "Bogotá", clase: "Carga suelta", grupo: grupo),   // 4: Carga suelta sin vehículo -> sin cambios
            Linea("Buenaventura", "Bogotá"));                                        // 5: sin clase de viaje       -> pendiente + motivo
        try
        {
            var l1 = Estado(id, 1); var l2 = Estado(id, 2); var l3 = Estado(id, 3); var l4 = Estado(id, 4); var l5 = Estado(id, 5);

            // Pendientes, con el motivo literal y SIN precio.
            foreach (var l in new[] { l1, l5 })
            {
                Assert.Equal(EstadoLinea.PorCostear, l.Estado);
                Assert.Equal(MotivoSinVehiculo, l.Motivo);
                Assert.False(l.ConFlete, "una línea sin vehículo no se autocostea con ningún precio");
            }
            // Las demás NO llevan ese motivo (se costean como siempre: auto o consulta completa por sus propias reglas).
            foreach (var l in new[] { l2, l3, l4 })
                Assert.NotEqual(MotivoSinVehiculo, l.Motivo);

            // S5-HU-20.5 parte 1, en el mismo pliego (una captura real cuesta ~30 s de consultas a SICE-TAC/SAP):
            // el GET trae numeroLinea y las líneas salen en el orden en que se enviaron, igual en cada consulta.
            var primera = (await Costeo(id)).GetProperty("lineas").EnumerateArray().ToList();
            var segunda = (await Costeo(id)).GetProperty("lineas").EnumerateArray().ToList();
            Assert.Equal([1, 2, 3, 4, 5], primera.Select(l => l.GetProperty("numeroLinea").GetInt32()));
            Assert.Equal(primera.Select(l => l.GetProperty("lineaId").GetGuid()), segunda.Select(l => l.GetProperty("lineaId").GetGuid()));
            Assert.Equal([2, 3, 4], primera.Where(l => l.GetProperty("grupoRutaId").ValueKind != JsonValueKind.Null)
                .Select(l => l.GetProperty("numeroLinea").GetInt32()));
        }
        finally { Limpiar(id, vehiculo); }
    }

    [Fact]
    public async Task La_linea_de_Cupo_sin_vehiculo_sigue_visible_en_la_Mesa_con_motivo_y_al_ponerle_vehiculo_se_costea_normal()
    {
        var vehiculo = $"T297-{Guid.NewGuid():N}"[..14];
        var id = await Capturar(Linea("Buenaventura", "Bogotá", clase: "Cupo"));
        try
        {
            var (estado, motivo, _) = Estado(id, 1);
            Assert.Equal(EstadoLinea.PorCostear, estado);
            Assert.Equal(MotivoSinVehiculo, motivo);

            // La propuesta sigue devolviendo los candidatos como referencia y marca el modo: la Mesa la muestra.
            var antes = (await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{id}/propuesta-tarifa")).GetProperty("lineas")[0];
            Assert.Equal("ConsultaCompleta", antes.GetProperty("modoDecision").GetString());
            Assert.Equal(MotivoSinVehiculo, antes.GetProperty("motivoPendiente").GetString());

            // "Una línea a la que luego se le pone vehículo se costea normalmente".
            _fx.WithDb(db =>
            {
                var veh = new Nexo.Domain.Catalogos.Entidades.clsConfiguracionVehiculo { TipoVehiculo = vehiculo };
                db.ConfiguracionesVehiculo.Add(veh);
                db.LineasSolicitud.Single(x => x.SolicitudId == id).ConfigVehiculoId = veh.Id;
                return db.SaveChanges();
            });

            var despues = (await _fx.Client().GetFromJsonAsync<JsonElement>($"/api/solicitudes/{id}/propuesta-tarifa")).GetProperty("lineas")[0];
            Assert.Equal(JsonValueKind.Null, despues.GetProperty("motivoPendiente").ValueKind);
        }
        finally { Limpiar(id, vehiculo); }
    }
}
