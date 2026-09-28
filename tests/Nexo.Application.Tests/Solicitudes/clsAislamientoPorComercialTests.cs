using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Bandeja;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// Issue #199 (S4-HU-19.1): un Comercial podía abrir el costeo o rechazar la solicitud
/// de OTRO comercial con solo conocer su Id — ninguno de los dos casos de uso
/// comprobaba que <c>CreadoPor</c> coincidiera con el usuario autenticado. Estas
/// pruebas usan dos comerciales con Id deliberadamente distintos (mismo espíritu que
/// clsNotificarEntregaCommandTests: si algún día alguien vuelve a colar acceso a un
/// recurso ajeno, una prueba con un solo usuario no lo detectaría) y confirman que
/// Mesa de Costeo (CoordinadorFletes) y Admin no quedan restringidos.
/// </summary>
public class clsAislamientoPorComercialTests
{
    private static readonly Guid ComercialA = Guid.NewGuid();
    private static readonly Guid ComercialB = Guid.NewGuid();
    private static readonly Guid SolicitudId = Guid.NewGuid();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    private static clsSolicitud SolicitudDeA() => new()
    {
        Id = SolicitudId,
        CreadoPor = ComercialA,
        Estado = EstadoSolicitud.Pendiente,
    };

    private static Mock<ICurrentUserPort> Usuario(Guid id, RolUsuario rol)
    {
        var m = new Mock<ICurrentUserPort>();
        m.SetupGet(u => u.Id).Returns(id);
        m.SetupGet(u => u.Rol).Returns(rol);
        return m;
    }

    // ------------------------------------------------------------------ costeo

    private clsObtenerCosteoQueryHandler CosteoHandler(
        Mock<ISolicitudesRepository> repo, Mock<ICurrentUserPort> usuario, Mock<IUnitOfWork>? uow = null) =>
        new(repo.Object, Mock.Of<ITarifasCosteoPort>(), Reloj, (uow ?? new Mock<IUnitOfWork>()).Object, usuario.Object);

    [Fact]
    public async Task El_dueno_de_la_solicitud_si_puede_abrir_su_costeo()
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerParaCosteoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        var dto = await CosteoHandler(repo, Usuario(ComercialA, RolUsuario.Comercial))
            .Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.NotNull(dto);
    }

    [Fact]
    public async Task Un_comercial_distinto_no_puede_abrir_el_costeo_de_otro()
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerParaCosteoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            CosteoHandler(repo, Usuario(ComercialB, RolUsuario.Comercial))
                .Handle(new clsObtenerCosteoQuery(SolicitudId)));

        Assert.Equal("SOLICITUD_AJENA", ex.Code);
    }

    [Theory]
    [InlineData(RolUsuario.CoordinadorFletes)] // Mesa de Costeo: sin restricción (criterio 3 de la HU)
    [InlineData(RolUsuario.Admin)]
    public async Task Mesa_de_costeo_y_admin_pueden_abrir_el_costeo_de_cualquiera(RolUsuario rol)
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerParaCosteoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        var dto = await CosteoHandler(repo, Usuario(Guid.NewGuid(), rol))
            .Handle(new clsObtenerCosteoQuery(SolicitudId));

        Assert.NotNull(dto);
    }

    // ----------------------------------------------------------------- rechazo

    private clsRechazarSolicitudCommandHandler RechazarHandler(
        Mock<ISolicitudesRepository> repo, Mock<ICurrentUserPort> usuario) => new(
        repo.Object, Mock.Of<IAuditoriaPort>(), usuario.Object, Reloj,
        Mock.Of<IPublicadorDeMensajes>(), Mock.Of<IUnitOfWork>());

    [Fact]
    public async Task Un_comercial_distinto_no_puede_rechazar_la_solicitud_de_otro()
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        var ex = await Assert.ThrowsAsync<clsForbiddenException>(() =>
            RechazarHandler(repo, Usuario(ComercialB, RolUsuario.Comercial))
                .Handle(new clsRechazarSolicitudCommand(SolicitudId, "precio", null)));

        Assert.Equal("SOLICITUD_AJENA", ex.Code);
    }

    [Fact]
    public async Task El_dueno_si_puede_rechazar_su_propia_solicitud()
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        await RechazarHandler(repo, Usuario(ComercialA, RolUsuario.Comercial))
            .Handle(new clsRechazarSolicitudCommand(SolicitudId, "precio", null));
        // No lanza: llegar hasta acá sin excepción es la aserción.
    }

    [Fact]
    public async Task Coordinador_de_fletes_puede_rechazar_la_solicitud_de_cualquier_comercial()
    {
        var repo = new Mock<ISolicitudesRepository>();
        repo.Setup(r => r.ObtenerBasicoAsync(SolicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SolicitudDeA());

        await RechazarHandler(repo, Usuario(Guid.NewGuid(), RolUsuario.CoordinadorFletes))
            .Handle(new clsRechazarSolicitudCommand(SolicitudId, "precio", null));
    }
}
