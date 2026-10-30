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
/// S5-HU-19.3/#283: no se puede entregar una cotización mientras queden líneas del pliego
/// sin costear (PorCostear). Tener al menos 1 flete no alcanza.
/// </summary>
public class clsEntregarCotizacionLineasPendientesTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ILineasSolicitudPort> _lineasSolicitud = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsEntregarCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _solicitudes.Object, _lineasSolicitud.Object, _auditoria.Object,
        _usuarioActual.Object, _publicador.Object, TimeProvider.System, _uow.Object);

    private Guid Preparar(params EstadoLinea[] estadosDeLasLineas) => Preparar(conFlete: true, estadosDeLasLineas);

    private Guid Preparar(bool conFlete, params EstadoLinea[] estadosDeLasLineas)
    {
        var cotizacionId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();
        var c = new clsCotizacion { Id = cotizacionId, SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        if (conFlete) c.Lineas.Add(new clsCotizacionLinea { ValorFlete = 1_800_000m, Fuente = FuenteFlete.Sicetac });
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(cotizacionId, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.EnCosteo });
        var lineas = estadosDeLasLineas.Select(e => new clsLineaSolicitud { SolicitudId = solicitudId, EstadoLinea = e })
            .ToDictionary(l => l.Id);
        _lineasSolicitud.Setup(l => l.ObtenerPorSolicitudAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, clsLineaSolicitud>)lineas);
        return cotizacionId;
    }

    [Fact]
    public async Task Con_una_linea_PorCostear_rechaza_con_COTIZACION_CON_LINEAS_PENDIENTES_y_no_publica_nada()
    {
        var id = Preparar(EstadoLinea.AutoResuelta, EstadoLinea.PorCostear);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEntregarCotizacionCommand(id, null, null)));

        Assert.Equal("COTIZACION_CON_LINEAS_PENDIENTES", ex.Code);
        _publicador.Verify(p => p.PublicarAsync(It.IsAny<Nexo.Mensajes.Notificaciones.clsCotizacionEntregada>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_todas_las_lineas_AutoResuelta_o_Revisada_entrega_normal()
    {
        var id = Preparar(EstadoLinea.AutoResuelta, EstadoLinea.Revisada);

        var r = await Handler().Handle(new clsEntregarCotizacionCommand(id, null, null));

        Assert.Equal("encolada", r.EstadoNotificacion);
    }

    [Fact]
    public async Task Sin_ninguna_linea_en_la_cotizacion_y_sin_rechazos_sigue_fallando_con_COTIZACION_SIN_FLETE()
    {
        var id = Preparar(conFlete: false, EstadoLinea.AutoResuelta);   // sin fletes y sin ninguna rechazada: no hay nada que entregar

        await Assert.ThrowsAsync<Nexo.Application.CasosDeUso.Cotizaciones.Excepciones.clsCotizacionSinFleteException>(() =>
            Handler().Handle(new clsEntregarCotizacionCommand(id, null, null)));
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------------------------- S6-HU-24.3/#320: líneas rechazadas

    // Una línea RECHAZADA cuenta como resuelta: es una decisión, no una pendiente.
    [Fact]
    public async Task Una_linea_rechazada_cuenta_como_resuelta_y_la_cotizacion_se_entrega()
    {
        var id = Preparar(EstadoLinea.AutoResuelta, EstadoLinea.Rechazada);

        var r = await Handler().Handle(new clsEntregarCotizacionCommand(id, null, null));

        Assert.Equal("encolada", r.EstadoNotificacion);
        _publicador.Verify(p => p.PublicarAsync(It.IsAny<Nexo.Mensajes.Notificaciones.clsCotizacionEntregada>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Todas rechazadas: no hay ningún flete (hoy lo impide la regla de "al menos uno"), pero se entrega igual, con los motivos.
    [Fact]
    public async Task Con_todas_las_lineas_rechazadas_y_ningun_flete_se_entrega_igual()
    {
        var id = Preparar(conFlete: false, EstadoLinea.Rechazada, EstadoLinea.Rechazada);

        var r = await Handler().Handle(new clsEntregarCotizacionCommand(id, null, null));

        Assert.Equal("encolada", r.EstadoNotificacion);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rechazadas_mas_una_pendiente_siguen_sin_poder_entregarse()
    {
        var id = Preparar(conFlete: false, EstadoLinea.Rechazada, EstadoLinea.PorCostear);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsEntregarCotizacionCommand(id, null, null)));

        Assert.Equal("COTIZACION_CON_LINEAS_PENDIENTES", ex.Code);
    }

    // El total no suma las rechazadas: no tienen fila de cotización, así que el total es solo el de los fletes.
    [Fact]
    public async Task El_total_de_la_cotizacion_entregada_no_suma_las_lineas_rechazadas()
    {
        var cotizacionId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();
        var c = new clsCotizacion
        {
            Id = cotizacionId, SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador,
            Lineas = { new clsCotizacionLinea { ValorFlete = 1_800_000m, Fuente = FuenteFlete.Sicetac } },
        };
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(cotizacionId, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.EnCosteo });
        var lineas = new[] { EstadoLinea.AutoResuelta, EstadoLinea.Rechazada, EstadoLinea.Rechazada }
            .Select(e => new clsLineaSolicitud { SolicitudId = solicitudId, EstadoLinea = e, MotivoRechazo = e == EstadoLinea.Rechazada ? "sin tarifa" : null })
            .ToDictionary(l => l.Id);
        _lineasSolicitud.Setup(l => l.ObtenerPorSolicitudAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, clsLineaSolicitud>)lineas);

        var r = await Handler().Handle(new clsEntregarCotizacionCommand(cotizacionId, null, null));

        Assert.Equal(1_800_000m, r.Cotizacion.Total);
    }
}
