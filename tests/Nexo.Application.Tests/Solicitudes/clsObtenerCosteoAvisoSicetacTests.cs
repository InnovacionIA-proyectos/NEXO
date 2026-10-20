using Moq;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// #300: cuando el piso SICE-TAC no se usa porque el período más reciente cargado no es el del mes actual, las líneas
/// salen con <c>pisoSicetac</c> null y Mesa de Costeo recibe un aviso que dice por qué (en vez de un "sin piso" mudo).
/// </summary>
public class clsObtenerCosteoAvisoSicetacTests
{
    private const string Motivo = "SICE-TAC no se usa: el período más reciente cargado es 202609 y el del mes actual es 202610 (sin mes de gracia).";
    private static readonly Guid Dueno = Guid.NewGuid();
    private static readonly Guid SolicitudId = Guid.NewGuid();

    private static clsSolicitud SolicitudConUnaLinea()
    {
        var solicitud = new clsSolicitud { Id = SolicitudId, CreadoPor = Dueno, Estado = EstadoSolicitud.Pendiente };
        solicitud.Lineas.Add(new clsLineaSolicitud
        {
            SolicitudId = SolicitudId, clsRuta = new clsRuta { Origen = "Cali", Destino = "Bogotá" }, Peso = 1000,
        });
        return solicitud;
    }

    private static clsObtenerCosteoQueryHandler Handler(Mock<ITarifasCosteoPort> tarifas)
        => Handler(tarifas, SolicitudConUnaLinea(), new HashSet<string>());

    private static clsObtenerCosteoQueryHandler Handler(
        Mock<ITarifasCosteoPort> tarifas, clsSolicitud solicitud, IReadOnlySet<string> clavesSoloSicetac)
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerParaCosteoAsync(SolicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(solicitud);
        var usuario = new Mock<ICurrentUserPort>();
        usuario.SetupGet(u => u.Id).Returns(Dueno);
        usuario.SetupGet(u => u.Rol).Returns(RolUsuario.Admin);
        var reglas = new Mock<IClavesSoloSicetacPort>();
        reglas.Setup(r => r.ClavesRutaVehiculoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(clavesSoloSicetac);
        return new(repo.Object, tarifas.Object, TimeProvider.System, new Mock<IUnitOfWork>().Object, usuario.Object, reglas.Object);
    }

    [Fact]
    public async Task Sin_piso_por_periodo_desactualizado_el_costeo_trae_el_aviso()
    {
        var tarifas = new Mock<ITarifasCosteoPort>();
        tarifas.Setup(t => t.PisoVigenteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsPisoSicetacDto?)null);
        tarifas.Setup(t => t.MotivoSicetacNoUsadoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Motivo);

        var dto = await Handler(tarifas).Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.Null(Assert.Single(dto.Lineas).PisoSicetac);
        Assert.Equal(Motivo, dto.AvisoSicetac);
    }

    [Fact]
    public async Task Con_piso_no_hay_aviso_y_ni_se_pregunta_por_el_motivo()
    {
        var tarifas = new Mock<ITarifasCosteoPort>();
        tarifas.Setup(t => t.PisoVigenteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<TipologiaCarga?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPisoSicetacDto(Guid.Empty, 5_000_000m, 90_000m, 8m, 202610, "vigente", "3S3", "General", "FURGON", "CARGADO"));

        var dto = await Handler(tarifas).Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.NotNull(Assert.Single(dto.Lineas).PisoSicetac);
        Assert.Null(dto.AvisoSicetac);
        tarifas.Verify(t => t.MotivoSicetacNoUsadoAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- S6-HU-26.1/#30: tieneReglaSoloSicetac

    private const string DaneOrigen = "11001000";
    private const string DaneDestino = "05001000";

    private static clsSolicitud SolicitudConRutaYVehiculo(string vehiculo)
    {
        var solicitud = new clsSolicitud { Id = SolicitudId, CreadoPor = Dueno, Estado = EstadoSolicitud.Pendiente };
        solicitud.Lineas.Add(new clsLineaSolicitud
        {
            SolicitudId = SolicitudId, Peso = 1000, ClaseViaje = ClaseViaje.Cupo,
            clsRuta = new clsRuta
            {
                Origen = "Bogotá", Destino = "Medellín",
                OrigenMunicipio = new clsMunicipio { CodigoDane = DaneOrigen },
                DestinoMunicipio = new clsMunicipio { CodigoDane = DaneDestino },
            },
            clsConfiguracionVehiculo = new clsConfiguracionVehiculo { TipoVehiculo = vehiculo },
        });
        return solicitud;
    }

    private static IReadOnlySet<string> Regla(string vehiculo) =>
        new HashSet<string> { Nexo.Domain.Common.clsTexto.ClaveRutaVehiculo(DaneOrigen, DaneDestino, vehiculo) };

    [Fact]
    public async Task La_linea_cuya_ruta_y_vehiculo_tienen_regla_Solo_SICE_sale_con_tieneReglaSoloSicetac_true()
    {
        var dto = await Handler(new Mock<ITarifasCosteoPort>(), SolicitudConRutaYVehiculo("3S3"), Regla("3S3"))
            .Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.True(Assert.Single(dto.Lineas).TieneReglaSoloSicetac);
    }

    [Fact]
    public async Task Con_regla_para_otro_vehiculo_de_la_ruta_sale_false()
    {
        var dto = await Handler(new Mock<ITarifasCosteoPort>(), SolicitudConRutaYVehiculo("3S3"), Regla("C2"))
            .Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.False(Assert.Single(dto.Lineas).TieneReglaSoloSicetac);
    }

    [Fact]
    public async Task Sin_ninguna_regla_cargada_sale_false()
    {
        var dto = await Handler(new Mock<ITarifasCosteoPort>(), SolicitudConRutaYVehiculo("3S3"), new HashSet<string>())
            .Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.False(Assert.Single(dto.Lineas).TieneReglaSoloSicetac);
    }
}
