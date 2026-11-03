using System.Net;
using System.Net.Http.Json;
using Nexo.Api.Tests.Infra;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-18/#262: clsPropuestaTarifaAdapter.ProponerAsync corre un grupo de líneas por
/// tarea en paralelo (Concurrencia=10). Antes, las consultas a tbl_tarifas_plaza/histórico/
/// rangos de peso (candidato "plaza"/"histórico" propio, S5-HU-16.3/#256) usaban el
/// clsNexoDbContext COMPARTIDO de la instancia en vez de uno con scope por tarea — EF Core
/// no es thread-safe, así que con varios grupos corriendo a la vez eso producía
/// InvalidOperationException intermitentes. Esta prueba arma una solicitud con muchas
/// líneas de rutas DISTINTAS (grupos distintos, todas con municipio resuelto para que
/// entren por el camino que toca tbl_tarifas_plaza/histórico) y confirma que la propuesta
/// completa sin errores técnicos filtrados al usuario.
/// </summary>
[Collection("api")]
public class clsPropuestaTarifaParalelaTests
{
    private readonly clsApiFixture _fx;
    public clsPropuestaTarifaParalelaTests(clsApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Solicitud_con_muchas_rutas_distintas_propone_todas_las_lineas_sin_errores_tecnicos()
    {
        // Pool de municipios reales de Colombia (ya usados en los tests de S5-HU-16) para
        // armar varias rutas distintas y así varios grupos ("huellas") en paralelo.
        var municipios = new[] { 1, 126, 149, 605, 657, 780, 832, 846, 1006, 1012 }; // Medellín, Barranquilla, Bogotá, Neiva, Sta Marta, Cúcuta, Pereira, Bucaramanga, Cali, Buenaventura

        var solicitudId = _fx.WithDb(db =>
        {
            var cliente = db.Clientes.First();
            var solicitud = new clsSolicitud
            {
                ClienteId = cliente.Id, Tipo = TipoSolicitud.Licitacion, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
            };
            var sufijo = Guid.NewGuid().ToString("N")[..8];

            for (var i = 0; i < municipios.Length - 1; i++)
            {
                var origenId = municipios[i];
                var destinoId = municipios[i + 1];
                var ruta = new clsRuta
                {
                    Origen = $"Origen{i} (paralela-{sufijo})", Destino = $"Destino{i} (paralela-{sufijo})",
                    OrigenMunicipioId = origenId, DestinoMunicipioId = destinoId,
                };
                var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = $"TIPO{i % 3}" };
                db.Rutas.Add(ruta);
                db.ConfiguracionesVehiculo.Add(vehiculo);
                solicitud.Lineas.Add(new clsLineaSolicitud
                {
                    SolicitudId = solicitud.Id, clsRuta = ruta, RutaId = ruta.Id,
                    clsConfiguracionVehiculo = vehiculo, ConfigVehiculoId = vehiculo.Id,
                    Peso = 8000 + i * 500,
                });
            }

            db.Solicitudes.Add(solicitud);
            db.SaveChanges();
            return solicitud.Id;
        });

        var resp = await _fx.Client().GetAsync($"/api/solicitudes/{solicitudId}/propuesta-tarifa");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var lineas = dto.GetProperty("lineas");
        Assert.Equal(municipios.Length - 1, lineas.GetArrayLength());

        foreach (var l in lineas.EnumerateArray())
        {
            var justificacion = l.GetProperty("justificacion").GetString() ?? "";
            Assert.DoesNotContain("A second operation", justificacion);
            Assert.DoesNotContain("System.", justificacion);
            Assert.DoesNotContain("InvalidOperationException", justificacion);
        }
    }
}
