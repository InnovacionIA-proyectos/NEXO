using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-16.3/#256: candidato "plaza" contra nuestra propia tbl_tarifas_plaza (sembrada
/// en S4-HU-10.1/10.2) — Buenaventura(1012)→Pereira(832), vehículo 2S2, peso 10000 (banda
/// PesoId=1), es una fila real de esa tabla (Valor=3.300.000).
///
/// S5-HU-19.1/#281 cambió el ORDEN de las fuentes de plaza: antes de esta issue, "plaza
/// propia" se consultaba ANTES que el selector tolerante de SAP (zona) — ganaba siempre
/// que existiera, sin competir con SAP. El flujo correcto es SAP oficial -&gt; SAP
/// tolerante -&gt; propia, ganando la primera VIGENTE (externa primero) — y la base de
/// pruebas real SÍ tiene una fila SAP vigente para esta misma zona (ZC_BUENAVENTURA -&gt;
/// ZTC_PEREIRA, vehículo SAP 2, Valor=3.000.000, verificado contra tblSAPTarifas) que
/// ahora gana legítimamente, antes de llegar a la propia. Este test ya NO prueba que la
/// propia gana — prueba que SAP gana cuando tiene una vigente, que es exactamente la regla
/// nueva. La prueba de que la propia SÍ se consulta y SÍ se usa (cuando SAP no tiene nada)
/// vive en clsTarifaPlazaInternaE2ETests, contra el puerto directamente.
/// </summary>
[Collection("api")]
public class clsPropuestaPlazaPropiaTests(clsApiFixture fx)
{
    [Fact]
    public async Task Linea_con_una_tarifa_SAP_vigente_para_la_zona_le_gana_a_la_tarifa_de_plaza_propia()
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
        var decision = await propuestas.ProponerParaLineaAsync(lineaId);

        Assert.Equal("plaza", decision.Resultado.Fuente);
        Assert.Equal(3_000_000m, decision.Resultado.Valor);
    }
}
