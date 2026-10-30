using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Solicitudes;
using Moq;

namespace Nexo.Application.Tests.Solicitudes;

/// <summary>
/// La carga de un pliego no cuesta las líneas: las guarda y encola el trabajo. Estas
/// pruebas fijan ese contrato — qué se publica, con qué correlación, y que nada se
/// publique cuando la carga no procede.
/// </summary>
public class clsCargarLineasBulkCommandTests
{
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IRegistroDeTrabajos> _trabajos = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly clsFakeTimeProvider _clock = new();

    private static readonly Guid UsuarioId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private clsCargarLineasBulkCommandHandler Handler()
    {
        _usuarioActual.SetupGet(u => u.Id).Returns(UsuarioId);
        return new(_solicitudes.Object, _auditoria.Object, _usuarioActual.Object,
                   _publicador.Object, _trabajos.Object, _clock, _uow.Object);
    }

    private static clsCargarLineasBulkCommand Comando(Guid solicitudId, int lineas = 3) =>
        new(solicitudId, Enumerable.Range(0, lineas)
            .Select(_ => new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null))
            .ToList());

    [Fact]
    public async Task Cargar_lineas_publica_un_AutoCosteoSolicitado_con_el_trabajo_devuelto()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        clsAutoCosteoSolicitado? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsAutoCosteoSolicitado>(), default))
            .Callback<clsAutoCosteoSolicitado, CancellationToken>((m, _) => publicado = m)
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
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

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
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        var orden = new List<string>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsAutoCosteoSolicitado>(), default))
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
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

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
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Entregada });

        await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando(solicitudId)));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsAutoCosteoSolicitado>(), default), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
        _trabajos.Verify(t => t.Registrar(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Solicitud_inexistente_no_publica_nada()
    {
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(It.IsAny<Guid>(), default)).ReturnsAsync((clsSolicitud?)null);

        await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando(Guid.NewGuid())));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsAutoCosteoSolicitado>(), default), Times.Never);
    }

    // S5-HU-17/#263: antes, clsCrearLineaInput/clsCargarLineasBulkCommand no tenían
    // HorasCargue/HorasDescargue — se descartaban en silencio y la línea siempre caía al
    // default duro, sin importar lo que el Comercial hubiera escrito en el formulario.
    [Fact]
    public async Task Linea_con_horas_propias_las_conserva()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        var comando = new clsCargarLineasBulkCommand(solicitudId,
            [new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null, HorasCargue: 6m, HorasDescargue: 2m)]);
        await Handler().Handle(comando);

        var linea = Assert.Single(guardadas!);
        Assert.Equal(6m, linea.HorasCargue);
        Assert.Equal(2m, linea.HorasDescargue);
    }

    [Fact]
    public async Task Linea_sin_horas_usa_el_default_4_4_cuando_la_solicitud_no_tiene_condiciones_propias()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente, CondicionesServicio = null });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        var comando = new clsCargarLineasBulkCommand(solicitudId,
            [new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null)]);
        await Handler().Handle(comando);

        var linea = Assert.Single(guardadas!);
        Assert.Equal(4m, linea.HorasCargue);
        Assert.Equal(4m, linea.HorasDescargue);
    }

    [Fact]
    public async Task Linea_sin_horas_usa_las_de_la_solicitud_cuando_esta_si_las_tiene()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud
            {
                Id = solicitudId, Estado = EstadoSolicitud.Pendiente,
                CondicionesServicio = "{\"tiemposLogisticos\":{\"horasCargue\":8,\"horasDescargue\":3}}",
            });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        var comando = new clsCargarLineasBulkCommand(solicitudId,
            [new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null)]);
        await Handler().Handle(comando);

        var linea = Assert.Single(guardadas!);
        Assert.Equal(8m, linea.HorasCargue);
        Assert.Equal(3m, linea.HorasDescargue);
    }

    // S5-HU-18.3/#275: GrupoRutaId lo pone SIEMPRE Cotifly — este handler solo lo guarda
    // tal cual, nunca lo calcula.
    [Fact]
    public async Task Linea_con_GrupoRutaId_lo_guarda_tal_cual()
    {
        var solicitudId = Guid.NewGuid();
        var grupoRutaId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        var comando = new clsCargarLineasBulkCommand(solicitudId,
            [new clsCrearLineaInput(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, null, GrupoRutaId: grupoRutaId)]);
        await Handler().Handle(comando);

        Assert.Equal(grupoRutaId, Assert.Single(guardadas!).GrupoRutaId);
    }

    [Fact]
    public async Task Linea_sin_GrupoRutaId_queda_null()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        await Handler().Handle(Comando(solicitudId, lineas: 1));

        Assert.Null(Assert.Single(guardadas!).GrupoRutaId);
    }

    // S5-HU-18.3/#275: el orden importa (los conexos de una ruta se cuelgan de la primera
    // línea del grupo) — el lote se numera 1..N en el orden en que llegó.
    [Fact]
    public async Task Las_lineas_del_lote_quedan_numeradas_en_orden_de_llegada()
    {
        var solicitudId = Guid.NewGuid();
        _solicitudes.Setup(r => r.ObtenerBasicoAsync(solicitudId, default))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, Estado = EstadoSolicitud.Pendiente });

        List<clsLineaSolicitud>? guardadas = null;
        _solicitudes.Setup(r => r.AgregarLineas(It.IsAny<IEnumerable<clsLineaSolicitud>>()))
            .Callback<IEnumerable<clsLineaSolicitud>>(ls => guardadas = ls.ToList());

        await Handler().Handle(Comando(solicitudId, lineas: 3));

        Assert.Equal([1, 2, 3], guardadas!.Select(l => l.NumeroLinea));
    }
}
