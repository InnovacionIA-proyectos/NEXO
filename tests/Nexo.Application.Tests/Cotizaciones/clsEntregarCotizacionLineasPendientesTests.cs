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

    private Guid Preparar(params EstadoLinea[] estadosDeLasLineas)
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
    public async Task Sin_ninguna_linea_en_la_cotizacion_sigue_fallando_con_COTIZACION_SIN_FLETE()
    {
        var cotizacionId = Guid.NewGuid();
        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(cotizacionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsCotizacion { Id = cotizacionId, Estado = EstadoCotizacion.Borrador });

        await Assert.ThrowsAnyAsync<Exception>(() => Handler().Handle(new clsEntregarCotizacionCommand(cotizacionId, null, null)));
        _lineasSolicitud.Verify(l => l.ObtenerPorSolicitudAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
