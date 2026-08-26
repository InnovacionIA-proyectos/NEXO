using CabinaFletes.Application.CasosDeUso.Cotizaciones;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.Cotizaciones;

public class CrearCotizacionCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private CrearCotizacionCommandHandler Handler() => new(
        _cotizaciones.Object, _solicitudes.Object, _auditoria.Object, _usuarioActual.Object, _uow.Object);

    [Fact]
    public async Task Solicitud_inexistente_lanza_ValidationFailedException()
    {
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Solicitud?)null);

        await Assert.ThrowsAsync<ValidationFailedException>(
            () => Handler().Handle(new CrearCotizacionCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Reutiliza_el_borrador_existente_en_vez_de_crear_otro()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        var borrador = new Cotizacion { SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Cotizacion, bool>>>(), default))
            .ReturnsAsync(borrador);

        var dto = await Handler().Handle(new CrearCotizacionCommand(solicitudId));

        Assert.Equal(borrador.Id, dto.Id);
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<Cotizacion>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Solicitud_entregada_reabre_su_cotizacion_entregada()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Entregada });

        var entregada = new Cotizacion { SolicitudId = solicitudId, Estado = EstadoCotizacion.Entregada };
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Cotizacion, bool>>>(), default))
            .ReturnsAsync(entregada);

        var dto = await Handler().Handle(new CrearCotizacionCommand(solicitudId));

        Assert.Equal(entregada.Id, dto.Id);
        Assert.Equal(EstadoCotizacion.Entregada.ToString(), dto.Estado.ToString());
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<Cotizacion>()), Times.Never);
    }

    [Fact]
    public async Task Sin_cotizacion_previa_crea_un_borrador_nuevo()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Cotizacion, bool>>>(), default))
            .ReturnsAsync((Cotizacion?)null);

        var dto = await Handler().Handle(new CrearCotizacionCommand(solicitudId));

        Assert.Equal(solicitudId, dto.SolicitudId);
        _cotizaciones.Verify(c => c.Agregar(It.Is<Cotizacion>(x => x.SolicitudId == solicitudId)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
