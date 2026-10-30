using Nexo.Application.CasosDeUso.Catalogos.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S6-HU-27.3/#322 (D9): cambiar el vehículo de una línea de la cotización. Solo en Borrador; el flete anterior se descarta y la línea vuelve a
/// por costear; se devuelve la propuesta recalculada con el vehículo nuevo.
/// </summary>
public class clsCambiarVehiculoLineaCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ILineasSolicitudPort> _lineas = new();
    private readonly Mock<IConfiguracionesVehiculoPort> _vehiculos = new();
    private readonly Mock<IAliasCatalogoPort> _alias = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private readonly clsConfiguracionVehiculo _anterior = new() { TipoVehiculo = "C2", Carroceria = "Estacas", EsCatalogoOficial = true };
    private readonly clsConfiguracionVehiculo _nuevo = new() { TipoVehiculo = "2S2", Carroceria = "Furgon", EsCatalogoOficial = true };

    public clsCambiarVehiculoLineaCommandTests()
    {
        _alias.Setup(a => a.MapaActivoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new Dictionary<string, string>());
        _vehiculos.Setup(v => v.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_anterior, _nuevo]);
        _vehiculos.Setup(v => v.ObtenerAsync(_nuevo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_nuevo);
    }

    private clsCambiarVehiculoLineaCommandHandler Handler() => new(
        _cotizaciones.Object, _lineas.Object, _vehiculos.Object, _alias.Object, _propuestas.Object,
        _auditoria.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private (clsCotizacion C, clsLineaSolicitud L) Preparar(
        EstadoCotizacion estado = EstadoCotizacion.Borrador, EstadoLinea estadoLinea = EstadoLinea.Revisada, bool conFlete = true,
        string? motivoPropuesta = null)
    {
        var solicitudId = Guid.NewGuid();
        var c = new clsCotizacion { Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado };
        var l = new clsLineaSolicitud { Id = Guid.NewGuid(), SolicitudId = solicitudId, EstadoLinea = estadoLinea, ConfigVehiculoId = _anterior.Id };
        if (conFlete)
        {
            c.Lineas.Add(new clsCotizacionLinea { CotizacionId = c.Id, LineaSolicitudId = l.Id, ValorFlete = 2_000_000m, Fuente = FuenteFlete.Manual });
            c.Total = 2_000_000m;
        }
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _lineas.Setup(x => x.ObtenerDeSolicitudAsync(l.Id, solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(l);
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId, [
                new clsPropuestaLineaDto(l.Id, "A → B", "2S2", 1000,
                    new clsCandidatoTarifaDto("sicetac", 3_100_000m, "", "", 1), null, null, 3_100_000m, "sicetac", "Solo piso.",
                    MotivoPendiente: motivoPropuesta),
            ], []));
        return (c, l);
    }

    [Fact]
    public async Task Cambia_el_vehiculo_descarta_el_flete_recalcula_el_total_y_devuelve_la_propuesta_con_el_piso_nuevo()
    {
        var (c, l) = Preparar();

        var r = await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null));

        Assert.Equal(_nuevo.Id, l.ConfigVehiculoId);
        Assert.True(r.CambioVehiculo);
        Assert.True(r.FleteEliminado);
        Assert.Empty(c.Lineas);
        Assert.Equal(0m, c.Total);
        Assert.Equal(EstadoLinea.PorCostear, l.EstadoLinea);
        Assert.Equal("porCostear", r.EstadoLinea);
        Assert.Equal(3_100_000m, r.Propuesta!.Sicetac!.Valor);         // el piso del vehículo NUEVO
        Assert.Contains("flete anterior", l.MotivoPendiente);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid?>(), "cotizaciones", c.Id, "cambiar_vehiculo_linea", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task La_propuesta_se_pide_despues_de_guardar_el_vehiculo_nuevo()
    {
        var (c, l) = Preparar();
        var orden = new List<string>();
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback(() => orden.Add("guardar")).ReturnsAsync(1);
        _propuestas.Setup(p => p.ProponerAsync(c.SolicitudId, It.IsAny<CancellationToken>())).Callback(() => orden.Add("proponer"))
            .ReturnsAsync(new clsPropuestaSolicitudDto(c.SolicitudId, [], []));

        await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null));

        Assert.Equal("guardar", orden[0]);      // la propuesta lee de la base: sin guardar antes, calcularía con el vehículo viejo
        Assert.Equal("proponer", orden[1]);
    }

    [Fact]
    public async Task Una_linea_sin_flete_conserva_su_estado_y_pierde_el_motivo_viejo()
    {
        var (c, l) = Preparar(estadoLinea: EstadoLinea.PorCostear, conFlete: false);
        l.MotivoPendiente = "Línea sin vehículo: requiere consulta completa";

        var r = await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null));

        Assert.True(r.CambioVehiculo);
        Assert.False(r.FleteEliminado);
        Assert.Equal(EstadoLinea.PorCostear, l.EstadoLinea);
        Assert.Null(l.MotivoPendiente);         // hablaba del vehículo anterior
    }

    [Fact]
    public async Task Si_la_propuesta_nueva_trae_un_motivo_de_pendiente_la_linea_lo_guarda()
    {
        var (c, l) = Preparar(estadoLinea: EstadoLinea.PorCostear, conFlete: false, motivoPropuesta: "SICE-TAC no se usa: el período no es el del mes actual.");

        await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null));

        Assert.Equal("SICE-TAC no se usa: el período no es el del mes actual.", l.MotivoPendiente);
    }

    [Fact]
    public async Task El_mismo_vehiculo_que_ya_tenia_no_toca_nada()
    {
        var (c, l) = Preparar();
        l.ConfigVehiculoId = _nuevo.Id;

        var r = await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null));

        Assert.False(r.CambioVehiculo);
        Assert.False(r.FleteEliminado);
        Assert.Single(c.Lineas);
        Assert.Equal(EstadoLinea.Revisada, l.EstadoLinea);
        _auditoria.Verify(a => a.Registrar(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Guid>(), "cambiar_vehiculo_linea", It.IsAny<string>()), Times.Never);
    }

    // ---------------------------------------------------------------- reglas de estado

    [Fact]
    public async Task Con_la_cotizacion_entregada_falla_409_y_no_guarda()
    {
        var (c, l) = Preparar(estado: EstadoCotizacion.Entregada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null)));

        Assert.Equal("COTIZACION_NO_EDITABLE", ex.Code);
        Assert.Equal(_anterior.Id, l.ConfigVehiculoId);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_linea_rechazada_no_cambia_de_vehiculo_409()
    {
        var (c, l) = Preparar(estadoLinea: EstadoLinea.Rechazada, conFlete: false);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, null, null)));

        Assert.Equal("LINEA_RECHAZADA", ex.Code);
    }

    [Fact]
    public async Task Cotizacion_o_linea_inexistente_falla_404()
    {
        var (c, _) = Preparar();

        var cot = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(Guid.NewGuid(), Guid.NewGuid(), _nuevo.Id, null, null)));
        var lin = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, Guid.NewGuid(), _nuevo.Id, null, null)));

        Assert.Equal("COTIZACION_NO_ENCONTRADA", cot.Code);
        Assert.Equal("LINEA_NO_ENCONTRADA", lin.Code);
    }

    // ---------------------------------------------------------------- cómo se indica el vehículo

    [Fact]
    public async Task Hay_que_indicar_el_vehiculo_con_uno_solo_de_los_dos_datos()
    {
        var (c, l) = Preparar();

        var ninguno = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, null, "  ", null)));
        var ambos = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, _nuevo.Id, "2S2", null)));

        Assert.Equal("VEHICULO_REQUERIDO", ninguno.Code);
        Assert.Equal("VEHICULO_REQUERIDO", ambos.Code);
    }

    [Fact]
    public async Task Por_id_solo_se_acepta_una_configuracion_oficial_que_exista()
    {
        var (c, l) = Preparar();
        var enCuarentena = new clsConfiguracionVehiculo { TipoVehiculo = "Rara" };
        _vehiculos.Setup(v => v.ObtenerAsync(enCuarentena.Id, It.IsAny<CancellationToken>())).ReturnsAsync(enCuarentena);
        _vehiculos.Setup(v => v.ObtenerAsync(It.Is<Guid>(g => g != enCuarentena.Id && g != _nuevo.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsConfiguracionVehiculo?)null);

        var noOficial = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, enCuarentena.Id, null, null)));
        var noExiste = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, Guid.NewGuid(), null, null)));

        Assert.Equal("VEHICULO_NO_OFICIAL", noOficial.Code);
        Assert.Equal("VEHICULO_NO_ENCONTRADO", noExiste.Code);
    }

    [Fact]
    public async Task Por_texto_con_alias_resuelve_el_codigo_y_usa_la_fila_oficial()
    {
        var (c, l) = Preparar();
        _alias.Setup(a => a.MapaActivoAsync(clsAliasElemento.TipoVehiculo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["MINIMULA"] = "2S2" });

        var r = await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, null, "Minimula", "Furgon"));

        Assert.Equal(_nuevo.Id, l.ConfigVehiculoId);
        Assert.Equal("2S2", r.TipoVehiculo);
    }

    [Fact]
    public async Task Por_texto_sin_alias_se_acepta_una_oficial_existente_pero_un_desconocido_falla_400_sin_crear_nada()
    {
        var (c, l) = Preparar();

        var ok = await Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, null, "2s2", "FURGON"));
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCambiarVehiculoLineaCommand(c.Id, l.Id, null, "Vehiculo inventado", null)));

        Assert.Equal(_nuevo.Id, ok.ConfigVehiculoId);
        Assert.Equal("VEHICULO_NO_RECONOCIDO", ex.Code);
        _vehiculos.Verify(v => v.Agregar(It.IsAny<clsConfiguracionVehiculo>()), Times.Never);   // nada en cuarentena
    }
}
