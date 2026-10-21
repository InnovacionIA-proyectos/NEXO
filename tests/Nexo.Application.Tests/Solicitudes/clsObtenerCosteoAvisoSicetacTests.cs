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

    // ---------------------------------------------------------------- S6-HU-24.1/#309 y S6-HU-29.1/#311: campos nuevos de la línea

    private static clsSolicitud SolicitudConLinea(Action<clsLineaSolicitud> ajustar)
    {
        var solicitud = new clsSolicitud { Id = SolicitudId, CreadoPor = Dueno, Estado = EstadoSolicitud.Pendiente };
        var linea = new clsLineaSolicitud
        {
            SolicitudId = SolicitudId, Peso = 1000,
            clsRuta = new clsRuta { Origen = "Bogotá", Destino = "Medellín", OrigenMunicipioId = 149, DestinoMunicipioId = 1012 },
        };
        ajustar(linea);
        solicitud.Lineas.Add(linea);
        return solicitud;
    }

    private static async Task<clsLineaCosteoDto> Linea(Action<clsLineaSolicitud> ajustar)
    {
        var dto = await Handler(new Mock<ITarifasCosteoPort>(), SolicitudConLinea(ajustar), new HashSet<string>())
            .Handle(new clsObtenerCosteoQuery(SolicitudId));
        return Assert.Single(dto.Lineas);
    }

    [Fact]
    public async Task Una_linea_de_contenedor_trae_linea_de_negocio_clase_de_viaje_y_clase_de_contenedor()
    {
        var l = await Linea(x =>
        {
            x.LineaNegocio = LineaNegocio.PuertoAInterior;
            x.ClaseViaje = ClaseViaje.Contenedor;
            x.ClaseContenedor = "CNT20.IT";
        });

        Assert.Equal("puertoAInterior", l.LineaNegocio);
        Assert.Equal("contenedor", l.ClaseViaje);
        Assert.Equal("CNT20.IT", l.ClaseContenedor);
    }

    // claseContenedor es null salvo que la clase de viaje sea Contenedor: en las demás el dato, si llegó, no aplica.
    [Theory]
    [InlineData(ClaseViaje.Cupo, "cupo")]
    [InlineData(ClaseViaje.CargaSuelta, "cargaSuelta")]
    [InlineData(ClaseViaje.Extradimensionado, "extradimensionado")]
    public async Task Fuera_de_contenedor_la_clase_de_contenedor_sale_null_aunque_la_linea_la_tenga(ClaseViaje clase, string esperado)
    {
        var l = await Linea(x =>
        {
            x.ClaseViaje = clase;
            x.ClaseContenedor = "CNT40.FT";   // p. ej. el nombre provisional del front en Extradimensionado
        });

        Assert.Equal(esperado, l.ClaseViaje);
        Assert.Null(l.ClaseContenedor);
    }

    [Fact]
    public async Task Una_linea_sin_esos_datos_los_trae_null()
    {
        var l = await Linea(_ => { });

        Assert.Null(l.LineaNegocio);
        Assert.Null(l.ClaseViaje);
        Assert.Null(l.ClaseContenedor);
    }

    [Fact]
    public async Task Trae_los_ids_de_municipio_de_la_ruta_o_null_si_la_ruta_no_resolvio()
    {
        var conMunicipios = await Linea(_ => { });
        Assert.Equal(149, conMunicipios.OrigenMunicipioId);
        Assert.Equal(1012, conMunicipios.DestinoMunicipioId);

        var sinMunicipios = await Linea(x => x.clsRuta = new clsRuta { Origen = "Texto libre", Destino = "Otro" });
        Assert.Null(sinMunicipios.OrigenMunicipioId);
        Assert.Null(sinMunicipios.DestinoMunicipioId);
    }

    [Theory]
    [InlineData(EstadoLinea.PorCostear, "porCostear")]
    [InlineData(EstadoLinea.AutoResuelta, "autoResuelta")]
    [InlineData(EstadoLinea.Revisada, "revisada")]
    public async Task Trae_el_estado_de_la_linea_con_minuscula_inicial(EstadoLinea estado, string esperado)
    {
        var l = await Linea(x => x.EstadoLinea = estado);

        Assert.Equal(esperado, l.EstadoLinea);
    }

    [Fact]
    public async Task Trae_el_motivo_por_el_que_la_linea_sigue_pendiente()
    {
        const string motivo = "Línea sin vehículo: requiere consulta completa";

        var l = await Linea(x => { x.EstadoLinea = EstadoLinea.PorCostear; x.MotivoPendiente = motivo; });

        Assert.Equal("porCostear", l.EstadoLinea);
        Assert.Equal(motivo, l.MotivoPendiente);
    }

    [Fact]
    public async Task Una_linea_ya_resuelta_no_trae_motivo_pendiente()
    {
        var l = await Linea(x => x.EstadoLinea = EstadoLinea.AutoResuelta);

        Assert.Null(l.MotivoPendiente);
    }

    // S6-HU-24.3/#320: la Mesa y Cotifly ven la línea rechazada y su motivo (Cotifly no debe dejar la cotización "pendiente" por ella).
    [Fact]
    public async Task Una_linea_rechazada_trae_estado_rechazada_y_el_motivo_del_rechazo()
    {
        var l = await Linea(x =>
        {
            x.EstadoLinea = EstadoLinea.Rechazada;
            x.MotivoRechazo = "No hay tarifa vigente para la ruta";
        });

        Assert.Equal("rechazada", l.EstadoLinea);
        Assert.Equal("No hay tarifa vigente para la ruta", l.MotivoRechazo);
        Assert.Null(l.MotivoPendiente);
    }

    [Fact]
    public async Task Una_linea_no_rechazada_trae_motivoRechazo_null()
    {
        var l = await Linea(x => x.EstadoLinea = EstadoLinea.AutoResuelta);

        Assert.Null(l.MotivoRechazo);
    }
}
