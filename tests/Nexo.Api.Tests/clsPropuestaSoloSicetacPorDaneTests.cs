using Microsoft.Extensions.DependencyInjection;
using Nexo.Api.Tests.Infra;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Api.Tests;

/// <summary>
/// S5-HU-16.2/#255: la regla "solo SICE-TAC" (tbl_reglas_solo_sicetac, Origen/Destino en
/// código DANE) nunca se aplicaba porque se comparaba contra el nombre de ciudad en texto
/// libre de clsRuta — la clave nunca coincidía. Con el municipio ya resuelto (S5-HU-16.1/
/// #254), la clave se arma con el código DANE real de ambos lados.
/// </summary>
[Collection("api")]
public class clsPropuestaSoloSicetacPorDaneTests
{
    private readonly clsApiFixture _fx;
    public clsPropuestaSoloSicetacPorDaneTests(clsApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Linea_con_ruta_y_vehiculo_de_una_regla_real_entra_por_el_camino_solo_sicetac()
    {
        // Toma una regla "solo SICE-TAC" real ya cargada en la base de pruebas, y resuelve
        // sus códigos DANE a un municipio real (Colombia) — exactamente lo que #254 ahora
        // persiste en clsRuta.OrigenMunicipioId/DestinoMunicipioId al capturar.
        var (origenId, destinoId, origenNombre, destinoNombre, tipoVehiculo) = _fx.WithDb(db =>
        {
            var regla = db.ReglasSoloSicetac
                .Join(db.Municipios.Where(m => m.Pais == "CO"), r => r.Origen, mo => mo.CodigoDane, (r, mo) => new { r, mo })
                .Join(db.Municipios.Where(m => m.Pais == "CO"), x => x.r.Destino, md => md.CodigoDane, (x, md) => new { x.r, OrigenM = x.mo, DestinoM = md })
                .OrderBy(x => x.r.Id)
                .First();
            return (regla.OrigenM.Id, regla.DestinoM.Id, regla.OrigenM.Municipio, regla.DestinoM.Municipio, regla.r.TipoVehiculo);
        });

        var lineaId = _fx.WithDb(db =>
        {
            var cliente = db.Clientes.First();
            var solicitud = new clsSolicitud
            {
                ClienteId = cliente.Id, Tipo = TipoSolicitud.Spot, Estado = EstadoSolicitud.Pendiente,
                CreadoPor = db.Usuarios.First().Id,
                CondicionesServicio = "{\"tiemposLogisticos\":{\"horasCargue\":4,\"horasDescargue\":4}}",
            };
            // Sufijo único por corrida: Origen/Destino tiene índice único (Origen, Destino)
            // y el texto de la ruta no se reutiliza entre pruebas (mismo criterio que
            // clsServiciosConexosTests: códigos/nombres con Guid para no colisionar).
            var sufijo = Guid.NewGuid().ToString("N")[..8];
            var ruta = new clsRuta
            {
                Origen = $"{origenNombre} (test255-{sufijo})", Destino = $"{destinoNombre} (test255-{sufijo})",
                OrigenMunicipioId = origenId, DestinoMunicipioId = destinoId,
            };
            var vehiculo = new clsConfiguracionVehiculo { TipoVehiculo = tipoVehiculo };
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

        using var scope = _fx.Factory.Services.CreateScope();
        var propuestas = scope.ServiceProvider.GetRequiredService<IPropuestaDeLineaPort>();
        var decision = await propuestas.ProponerParaLineaAsync(lineaId);

        // S5-HU-19.2/#282: con regla y CON piso, el modo es SoloSicetac y la justificación
        // sigue mencionando "SOLO SICE-TAC", sin comparar plaza/histórico — igual que
        // antes. Con regla pero SIN piso para esta ruta/periodo real, el modo ya no es
        // "atención manual": cae a ConsultaCompleta (ahora sí compara plaza/histórico),
        // así que solo se afirma el modo SoloSicetac cuando de verdad hay piso.
        if (decision.Modo == Nexo.Domain.Costeo.Reglas.ModoDecision.SoloSicetac)
            Assert.Contains("SOLO SICE-TAC", decision.Resultado.Justificacion, StringComparison.OrdinalIgnoreCase);
        else
            Assert.Equal(Nexo.Domain.Costeo.Reglas.ModoDecision.ConsultaCompleta, decision.Modo);
    }
}
