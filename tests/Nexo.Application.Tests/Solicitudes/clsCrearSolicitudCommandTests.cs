using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Bandeja;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

public class clsCrearSolicitudCommandTests
{
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IClientesPort> _clientes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private static readonly TimeProvider Clock = TimeProvider.System;

    private clsCrearSolicitudCommandHandler Handler() => new(
        _solicitudes.Object, _clientes.Object, _auditoria.Object, _usuarioActual.Object, Clock,
        _publicador.Object, _uow.Object);

    private void PermitirClienteYParametros(Guid clienteId)
    {
        _clientes.Setup(c => c.ExisteAsync(clienteId, default)).ReturnsAsync(true);
        _solicitudes.Setup(s => s.ParametrosScoreAsync(default)).ReturnsAsync(new clsParametrosScore());
    }

    [Fact]
    public async Task Cliente_inexistente_lanza_ValidationFailedException()
    {
        var clienteId = Guid.NewGuid();
        _clientes.Setup(c => c.ExisteAsync(clienteId, default)).ReturnsAsync(false);

        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Spot", null, null, null, null, null)));

        _solicitudes.Verify(s => s.Agregar(It.IsAny<clsSolicitud>()), Times.Never);
    }

    [Fact]
    public async Task Tipo_invalido_lanza_ValidationFailedException()
    {
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);

        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearSolicitudCommand(clienteId, "NoExiste", null, null, null, null, null)));
    }

    [Fact]
    public async Task Solicitud_valida_se_agrega_con_score_calculado_y_se_guarda()
    {
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);
        clsSolicitud? guardada = null;
        _solicitudes.Setup(s => s.Agregar(It.IsAny<clsSolicitud>())).Callback<clsSolicitud>(s => guardada = s);

        var id = await Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Licitacion", null, null, 10_000_000m, 0.5m, null));

        Assert.NotNull(guardada);
        Assert.Equal(id, guardada!.Id);
        Assert.Equal(clienteId, guardada.ClienteId);
        Assert.InRange(guardada.ScorePrioridad, 0, 100);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Con_linea_inicial_la_agrega_a_la_solicitud()
    {
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);
        clsSolicitud? guardada = null;
        _solicitudes.Setup(s => s.Agregar(It.IsAny<clsSolicitud>())).Callback<clsSolicitud>(s => guardada = s);

        var linea = new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), "CargaSeca", 500m, 1_000_000m);
        await Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Spot", null, null, null, null, linea));

        Assert.Single(guardada!.Lineas);
        Assert.Equal(linea.RutaId, guardada.Lineas.First().RutaId);
    }

    [Fact]
    public async Task Avisa_que_la_bandeja_cambio_al_dar_de_alta_una_solicitud()
    {
        // Es el criterio de la HU-05.2: una solicitud nueva entra priorizada y quien tenga
        // la bandeja abierta tiene que enterarse sin refrescar.
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);

        await Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Spot", null, null, null, null, null));

        _publicador.Verify(p => p.PublicarAsync(
            It.Is<clsBandejaCambio>(m => m.Motivo == clsMotivoDeCambio.SolicitudNueva),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_aviso_de_bandeja_se_publica_antes_de_guardar()
    {
        // Con el Outbox, publicar ANTES de SaveChanges es lo que mete el mensaje en la
        // misma transacción. Al revés podría avisarse un alta que después se revierte, o
        // guardarse una solicitud sin que nadie se entere.
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);
        var orden = new List<string>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsBandejaCambio>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Spot", null, null, null, null, null));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task Un_alta_rechazada_no_avisa_a_la_bandeja()
    {
        // Sería lo peor: la bandeja se refresca para mostrar exactamente lo mismo, porque
        // la solicitud nunca existió.
        var clienteId = Guid.NewGuid();
        _clientes.Setup(c => c.ExisteAsync(clienteId, default)).ReturnsAsync(false);

        await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearSolicitudCommand(clienteId, "Spot", null, null, null, null, null)));

        _publicador.VerifyNoOtherCalls();
    }
}
