using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Mensajes.Solicitudes;
using Moq;

namespace CabinaFletes.Application.Tests.Solicitudes;

/// <summary>
/// La carga de un pliego no cuesta las líneas: las guarda y encola el trabajo. Estas
/// pruebas fijan ese contrato — qué se publica, con qué correlación, y que nada se
/// publique cuando la carga no procede.
/// </summary>
public class CargarLineasBulkCommandTests
{
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IRegistroDeTrabajos> _trabajos = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly FakeTimeProvider _clock = new();

    private static readonly Guid UsuarioId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private CargarLineasBulkCommandHandler Handler()
    {
        _usuarioActual.SetupGet(u => u.Id).Returns(UsuarioId);
        return new(_solicitudes.Object, _auditoria.Object, _usuarioActual.Object,
                   _publicador.Object, _trabajos.Object, _clock, _uow.Object);
    }

    private static CargarLineasBulkCommand Comando(Guid solicitudId, int lineas = 3) =>
        new(solicitudId, Enumerable.Range(0, lineas)
            .Select(_ => new CrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null))
            .ToList());

    [Fact]
    public async Task Cargar_lineas_publica_un_AutoCosteoSolicitado_con_el_trabajo_devuelto()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        AutoCosteoSolicitado? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default))
            .Callback<AutoCosteoSolicitado, CancellationToken>((m, _) => publicado = m)
            .Returns(Task.CompletedTask);

        var trabajoId = await Handler().Handle(Comando(solicitudId));

        Assert.NotNull(publicado);
        Assert.Equal(trabajoId, publicado!.TrabajoId);
        Assert.Equal(solicitudId, publicado.SolicitudId);
        Assert.Equal(3, publicado.TotalLineas);
        Assert.Equal(UsuarioId, publicado.SolicitadoPor);
        Assert.Equal(_clock.Fijo, publicado.SolicitadoEn);
    }

    [Fact]
    public async Task El_identificador_devuelto_es_distinto_en_cada_carga()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        var primero = await Handler().Handle(Comando(solicitudId));
        var segundo = await Handler().Handle(Comando(solicitudId));

        Assert.NotEqual(Guid.Empty, primero);
        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public async Task Se_publica_antes_de_confirmar_para_que_el_Outbox_los_una_en_la_misma_transaccion()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        var orden = new List<string>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(default))
            .Callback(() => orden.Add("confirmar")).ReturnsAsync(1);

        await Handler().Handle(Comando(solicitudId));

        Assert.Equal(["publicar", "confirmar"], orden);
    }

    [Fact]
    public async Task El_trabajo_queda_registrado_antes_de_confirmar_para_que_sea_consultable_de_inmediato()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        var orden = new List<string>();
        _trabajos.Setup(t => t.Registrar(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()))
            .Callback(() => orden.Add("registrar"));
        _uow.Setup(u => u.SaveChangesAsync(default)).Callback(() => orden.Add("confirmar")).ReturnsAsync(1);

        var trabajoId = await Handler().Handle(Comando(solicitudId));

        // Sin esto habría una ventana en la que consultar el identificador que se acaba
        // de devolver responde 404.
        _trabajos.Verify(t => t.Registrar(trabajoId, solicitudId, UsuarioId, _clock.Fijo), Times.Once);
        Assert.Equal("registrar", orden[0]);
        Assert.Equal("confirmar", orden[^1]);
    }

    [Fact]
    public async Task Solicitud_ya_entregada_no_publica_nada()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new Solicitud { Id = solicitudId, Estado = EstadoSolicitud.Entregada });

        await Assert.ThrowsAsync<ConflictException>(() => Handler().Handle(Comando(solicitudId)));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
        _trabajos.Verify(t => t.Registrar(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Solicitud_inexistente_no_publica_nada()
    {
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Solicitud?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(Comando(Guid.NewGuid())));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<AutoCosteoSolicitado>(), default), Times.Never);
    }
}
