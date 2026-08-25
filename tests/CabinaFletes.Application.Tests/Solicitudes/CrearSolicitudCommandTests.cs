using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.Solicitudes;

public class CrearSolicitudCommandTests
{
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IClientesPort> _clientes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly TimeProvider Clock = TimeProvider.System;

    private CrearSolicitudCommandHandler Handler() => new(
        _solicitudes.Object, _clientes.Object, _auditoria.Object, _usuarioActual.Object, Clock, _uow.Object);

    private void PermitirClienteYParametros(Guid clienteId)
    {
        _clientes.Setup(c => c.ExisteAsync(clienteId, default)).ReturnsAsync(true);
        _solicitudes.Setup(s => s.ParametrosScoreAsync(default)).ReturnsAsync(new ParametrosScore());
    }

    [Fact]
    public async Task Cliente_inexistente_lanza_ValidationFailedException()
    {
        var clienteId = Guid.NewGuid();
        _clientes.Setup(c => c.ExisteAsync(clienteId, default)).ReturnsAsync(false);

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new CrearSolicitudCommand(clienteId, "Spot", null, null, null, null, null)));

        _solicitudes.Verify(s => s.Agregar(It.IsAny<Solicitud>()), Times.Never);
    }

    [Fact]
    public async Task Tipo_invalido_lanza_ValidationFailedException()
    {
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);

        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new CrearSolicitudCommand(clienteId, "NoExiste", null, null, null, null, null)));
    }

    [Fact]
    public async Task Solicitud_valida_se_agrega_con_score_calculado_y_se_guarda()
    {
        var clienteId = Guid.NewGuid();
        PermitirClienteYParametros(clienteId);
        Solicitud? guardada = null;
        _solicitudes.Setup(s => s.Agregar(It.IsAny<Solicitud>())).Callback<Solicitud>(s => guardada = s);

        var id = await Handler().Handle(new CrearSolicitudCommand(clienteId, "Licitacion", null, null, 10_000_000m, 0.5m, null));

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
        Solicitud? guardada = null;
        _solicitudes.Setup(s => s.Agregar(It.IsAny<Solicitud>())).Callback<Solicitud>(s => guardada = s);

        var linea = new CrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), "CargaSeca", 500m, 1_000_000m);
        await Handler().Handle(new CrearSolicitudCommand(clienteId, "Spot", null, null, null, null, linea));

        Assert.Single(guardada!.Lineas);
        Assert.Equal(linea.RutaId, guardada.Lineas.First().RutaId);
    }
}
