using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S6-HU-24.3/#320: rechazar una línea de la cotización con un motivo, y deshacerlo. Una línea rechazada nunca tiene fila de cotización.
/// </summary>
public class clsRechazarLineaCotizacionCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ILineasSolicitudPort> _lineas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly Guid Usuario = Guid.Parse("aaaa0000-0000-0000-0000-0000000000a1");

    public clsRechazarLineaCotizacionCommandTests() => _usuario.SetupGet(u => u.Id).Returns(Usuario);

    private clsRechazarLineaCotizacionCommandHandler Rechazar() =>
        new(_cotizaciones.Object, _lineas.Object, _auditoria.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private clsDeshacerRechazoLineaCotizacionCommandHandler Deshacer() =>
        new(_cotizaciones.Object, _lineas.Object, _auditoria.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private (clsCotizacion C, clsLineaSolicitud Linea) Preparar(
        EstadoCotizacion estado = EstadoCotizacion.Borrador, EstadoLinea estadoLinea = EstadoLinea.PorCostear, bool conFlete = false)
    {
        var solicitudId = Guid.NewGuid();
        var c = new clsCotizacion { Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado };
        var linea = new clsLineaSolicitud { Id = Guid.NewGuid(), SolicitudId = solicitudId, EstadoLinea = estadoLinea };
        if (estadoLinea == EstadoLinea.Rechazada)
        {
            linea.MotivoRechazo = "Sin tarifa vigente para la ruta";
            linea.RechazadaPor = Usuario;
            linea.RechazadaAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        }
        if (conFlete) c.Lineas.Add(new clsCotizacionLinea { LineaSolicitudId = linea.Id, ValorFlete = 1_000_000m, Fuente = FuenteFlete.Manual });
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _lineas.Setup(l => l.ObtenerDeSolicitudAsync(linea.Id, solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(linea);
        return (c, linea);
    }

    // ---------------------------------------------------------------- rechazar

    [Fact]
    public async Task Rechaza_la_linea_con_motivo_y_deja_quien_y_cuando()
    {
        var (c, linea) = Preparar();

        var r = await Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, "  No hay tarifa  para esa ruta  "));

        Assert.Equal(EstadoLinea.Rechazada, linea.EstadoLinea);
        Assert.Equal("No hay tarifa para esa ruta", linea.MotivoRechazo);   // limpio de espacios
        Assert.Equal(Usuario, linea.RechazadaPor);
        Assert.NotNull(linea.RechazadaAt);
        Assert.Equal("rechazada", r.EstadoLinea);
        Assert.Equal("No hay tarifa para esa ruta", r.MotivoRechazo);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditoria.Verify(a => a.Registrar(Usuario, "cotizaciones", c.Id, "rechazar_linea",
            It.Is<string>(d => d.Contains(linea.Id.ToString()) && d.Contains("No hay tarifa para esa ruta"))), Times.Once);
    }

    [Fact]
    public async Task Limpia_el_motivo_pendiente_porque_ya_no_esta_pendiente()
    {
        var (c, linea) = Preparar();
        linea.MotivoPendiente = "Consulta completa: el Administrador de Tarifas debe elegir la tarifa.";

        await Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, "Sin tarifa"));

        Assert.Null(linea.MotivoPendiente);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task Sin_motivo_responde_error_de_validacion_y_no_toca_la_linea(string? motivo)
    {
        var (c, linea) = Preparar();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, motivo)));

        Assert.Equal("MOTIVO_RECHAZO_REQUERIDO", ex.Code);
        Assert.Equal(EstadoLinea.PorCostear, linea.EstadoLinea);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_motivo_de_mas_de_500_caracteres_se_rechaza_en_vez_de_truncarse()
    {
        var (c, linea) = Preparar();

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, new string('x', 501))));

        Assert.Equal("MOTIVO_RECHAZO_REQUERIDO", ex.Code);
        Assert.Contains("500", ex.Message);
    }

    [Fact]
    public async Task Cotizacion_entregada_no_se_toca_409()
    {
        var (c, linea) = Preparar(estado: EstadoCotizacion.Entregada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, "x")));

        Assert.Equal("COTIZACION_NO_EDITABLE", ex.Code);
        Assert.Equal(EstadoLinea.PorCostear, linea.EstadoLinea);
    }

    // Una línea que ya tiene flete en la cotización no se rechaza: así una rechazada nunca suma al total ni sale con precio.
    [Fact]
    public async Task Una_linea_que_ya_tiene_flete_no_se_puede_rechazar_409()
    {
        var (c, linea) = Preparar(estadoLinea: EstadoLinea.Revisada, conFlete: true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, "x")));

        Assert.Equal("LINEA_CON_FLETE", ex.Code);
        Assert.Equal(EstadoLinea.Revisada, linea.EstadoLinea);
    }

    [Fact]
    public async Task Una_linea_ya_rechazada_no_se_vuelve_a_rechazar_409()
    {
        var (c, linea) = Preparar(estadoLinea: EstadoLinea.Rechazada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, linea.Id, "otro motivo")));

        Assert.Equal("LINEA_YA_RECHAZADA", ex.Code);
        Assert.Equal("Sin tarifa vigente para la ruta", linea.MotivoRechazo);   // el motivo original no se pisa
    }

    [Fact]
    public async Task Una_linea_de_otra_solicitud_da_404()
    {
        var (c, _) = Preparar();

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(c.Id, Guid.NewGuid(), "x")));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Cotizacion_inexistente_da_404()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Rechazar().Handle(new clsRechazarLineaCotizacionCommand(Guid.NewGuid(), Guid.NewGuid(), "x")));

        Assert.Equal("COTIZACION_NO_ENCONTRADA", ex.Code);
    }

    // ---------------------------------------------------------------- deshacer

    [Fact]
    public async Task Deshacer_devuelve_la_linea_a_por_costear_y_limpia_motivo_quien_y_cuando()
    {
        var (c, linea) = Preparar(estadoLinea: EstadoLinea.Rechazada);

        var r = await Deshacer().Handle(new clsDeshacerRechazoLineaCotizacionCommand(c.Id, linea.Id));

        Assert.Equal(EstadoLinea.PorCostear, linea.EstadoLinea);
        Assert.Null(linea.MotivoRechazo);
        Assert.Null(linea.RechazadaPor);
        Assert.Null(linea.RechazadaAt);
        Assert.NotNull(linea.MotivoPendiente);   // vuelve a estar por costear, y la Mesa dice por qué
        Assert.Equal("porCostear", r.EstadoLinea);
        _auditoria.Verify(a => a.Registrar(Usuario, "cotizaciones", c.Id, "deshacer_rechazo_linea",
            It.Is<string>(d => d.Contains("Sin tarifa vigente para la ruta"))), Times.Once);   // el rechazo anterior queda auditado
    }

    // Una vez entregada, el comercial ya recibió el correo con el motivo: deshacer solo se puede en la Mesa, antes de entregar.
    [Fact]
    public async Task Deshacer_con_la_cotizacion_entregada_da_409_y_no_cambia_nada()
    {
        var (c, linea) = Preparar(estado: EstadoCotizacion.Entregada, estadoLinea: EstadoLinea.Rechazada);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Deshacer().Handle(new clsDeshacerRechazoLineaCotizacionCommand(c.Id, linea.Id)));

        Assert.Equal("COTIZACION_NO_EDITABLE", ex.Code);
        Assert.Equal(EstadoLinea.Rechazada, linea.EstadoLinea);
        Assert.Equal("Sin tarifa vigente para la ruta", linea.MotivoRechazo);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deshacer_una_linea_que_no_esta_rechazada_da_409()
    {
        var (c, linea) = Preparar();

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Deshacer().Handle(new clsDeshacerRechazoLineaCotizacionCommand(c.Id, linea.Id)));

        Assert.Equal("LINEA_NO_RECHAZADA", ex.Code);
    }

    [Fact]
    public async Task Deshacer_una_linea_de_otra_solicitud_da_404()
    {
        var (c, _) = Preparar(estadoLinea: EstadoLinea.Rechazada);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Deshacer().Handle(new clsDeshacerRechazoLineaCotizacionCommand(c.Id, Guid.NewGuid())));

        Assert.Equal("LINEA_NO_ENCONTRADA", ex.Code);
    }
}
