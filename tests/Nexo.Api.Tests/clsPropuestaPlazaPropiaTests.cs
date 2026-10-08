using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-16.3/#256: antes de esta issue, el candidato "plaza" solo consultaba la tabla
/// externa SAP (tblSAPTarifas) — nuestra propia tbl_tarifas_plaza (sembrada en
/// S4-HU-10.1/10.2) nunca se tocaba. Buenaventura(1012)→Pereira(832), vehículo 2S2, es
/// una fila real de esa tabla (Valor=3.300.000) — verificado que el motor completo
/// (ProponerParaLineaAsync) la encuentra y le gana al piso SICE-TAC real de esa ruta
/// ($1.935.512), sin necesitar tblSAPTarifas.
/// </summary>
[Collection("api")]
public class clsPropuestaPlazaPropiaTests(clsApiFixture fx)
{
    [Fact]
    public async Task Linea_con_municipios_de_una_tarifa_de_plaza_propia_usa_esa_tarifa()
    {
        var lineaId = fx.WithDb(db =>
        {
            var cliente = db.Clientes.First();
            var solicitud = new clsSolicitud
            {
                ClienteId = cliente.Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
                CondicionesServicio = "{\"tiemposLogisticos\":{\"horasCargue\":4,\"horasDescargue\":4}}",
            };
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var ruta = new clsRuta
            {
                Origen = $"Buenaventura (test256-{sufijo})", Destino = $"Pereira (test256-{sufijo})",
                OrigenMunicipioId = 1012, DestinoMunicipioId = 832,
            };
            var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = "2S2" };
            var linea = new clsLineaSolicitud
            {
                SolicitudId = solicitud.Id, clsRuta = ruta, RutaId = ruta.Id,
                clsConfiguracionVehiculo = vehiculo, ConfigVehiculoId = vehiculo.Id,
                Peso = 10000,
            };
            solicitud.Lineas.Add(linea);
            db.Rutas.Add(ruta);
            db.ConfiguracionesVehiculo.Add(vehiculo);
            db.Solicitudes.Add(solicitud);
            db.SaveChanges();
            return linea.Id;
        });

        using var scope = fx.Factory.Services.CreateScope();
        var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaDeLineaPort>();
        var resultado = await propuestas.ProponerParaLineaAsync(lineaId);

        Assert.Equal("plaza", resultado.Fuente);
        Assert.Equal(3_300_000m, resultado.Valor);
    }
}
