using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Catalogos.Entidades;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Nexo.Mensajes.Notificaciones;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// S5-HU-20.1/#286 (HU-B23/B24): agregar y quitar servicios conexos se admite mientras la
/// cotización no tenga resultado (Borrador o Entregada); cambiarla ya entregada deja una marca
/// aparte (sin tocar Estado ni EntregadaAt) y avisa a quien creó la solicitud SOLO si otra
/// persona hizo el cambio.
/// </summary>
public class clsConexosTrasEntregaTests
{
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _creador = Guid.NewGuid();

    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IServiciosConexosPort> _servicios = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public clsConexosTrasEntregaTests() => _usuario.Setup(u => u.Id).Returns(_actor);

    private clsAgregarConexosCotizacionCommandHandler Agregar() => new(
        _cotizaciones.Object, _servicios.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object,
        _usuario.Object, TimeProvider.System, _uow.Object);

    private clsEliminarConexoCotizacionCommandHandler Quitar() => new(
        _cotizaciones.Object, _solicitudes.Object, _auditoria.Object, _publicador.Object,
        _usuario.Object, TimeProvider.System, _uow.Object);

    private static readonly DateTimeOffset EntregadaEn = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private (clsCotizacion C, Guid LineaId, clsServicioConexo Servicio, clsCotizacionConexo Existente) Preparar(
        EstadoCotizacion estado, ResultadoCotizacion resultado = ResultadoCotizacion.SinDefinir, Guid? creadorSolicitud = null)
    {
        var solicitudId = Guid.NewGuid();
        var lineaId = Guid.NewGuid();
        var servicio = new clsServicioConexo
        {
            Id = Guid.NewGuid(), CodigoServicio = "CARGUE", TipoServicio = "Cargue", CostoUnitario = 50_000m,
            Unidad = "Por unidad", Vigencia = DateOnly.FromDateTime(DateTime.UtcNow), Activo = true,
        };
        var c = new clsCotizacion
        {
            Id = Guid.NewGuid(), SolicitudId = solicitudId, Estado = estado, Resultado = resultado,
            EntregadaAt = estado == EstadoCotizacion.Entregada ? EntregadaEn : null,
        };
        c.Lineas.Add(new clsCotizacionLinea { Id = lineaId, CotizacionId = c.Id, ValorFlete = 1_000_000m });
        var existente = new clsCotizacionConexo
        {
            Id = Guid.NewGuid(), CotizacionId = c.Id, CotizacionLineaId = lineaId, ServicioConexoId = servicio.Id,
            clsServicioConexo = servicio, Cantidad = 2, ValorAplicado = 100_000m,
        };
        c.Conexos.Add(existente);
        c.Total = 1_100_000m;

        _cotizaciones.Setup(r => r.ObtenerConLineasYConexosAsync(c.Id, It.IsAny<CancellationToken>())).ReturnsAsync(c);
        _servicios.Setup(s => s.ObtenerPorIdAsync(servicio.Id, It.IsAny<CancellationToken>())).ReturnsAsync(servicio);
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsSolicitud { Id = solicitudId, CreadoPor = creadorSolicitud ?? _creador });
        return (c, lineaId, servicio, existente);
    }

    private clsAgregarConexosCotizacionCommand ComandoAgregar(clsCotizacion c, Guid lineaId, clsServicioConexo s) =>
        new(c.Id, [new clsConexoItem(lineaId, s.Id, 3, null)]);

    // ----------------------------------------------------- estados permitidos / cerrados

    [Theory]
    [InlineData(EstadoCotizacion.Borrador)]
    [InlineData(EstadoCotizacion.Entregada)]
    public async Task Agregar_se_admite_en_Borrador_y_en_Entregada(EstadoCotizacion estado)
    {
        var (c, lineaId, servicio, _) = Preparar(estado);

        var dto = await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        Assert.Equal(2, dto.Conexos.Count);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(EstadoCotizacion.Borrador)]
    [InlineData(EstadoCotizacion.Entregada)]
    public async Task Quitar_se_admite_en_Borrador_y_en_Entregada(EstadoCotizacion estado)
    {
        var (c, _, _, existente) = Preparar(estado);

        var dto = await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id));

        Assert.Empty(dto.Conexos);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ResultadoCotizacion.Ganada)]
    [InlineData(ResultadoCotizacion.Perdida)]
    public async Task Con_Ganada_o_Perdida_agregar_y_quitar_dan_COTIZACION_CERRADA_y_no_cambian_nada(ResultadoCotizacion resultado)
    {
        var (c, lineaId, servicio, existente) = Preparar(EstadoCotizacion.Entregada, resultado);

        var exAgregar = await Assert.ThrowsAsync<clsConflictException>(() => Agregar().Handle(ComandoAgregar(c, lineaId, servicio)));
        var exQuitar = await Assert.ThrowsAsync<clsConflictException>(() => Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id)));

        Assert.Equal("COTIZACION_CERRADA", exAgregar.Code);
        Assert.Equal("COTIZACION_CERRADA", exQuitar.Code);
        Assert.Single(c.Conexos);
        Assert.Null(c.ModificadaTrasEntregaAt);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----------------------------------------------------- quitar: casos de borde y total

    [Fact]
    public async Task Quitar_recalcula_el_total()
    {
        var (c, _, _, existente) = Preparar(EstadoCotizacion.Borrador);

        var dto = await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id));

        Assert.Equal(1_000_000m, dto.Total);
    }

    [Fact]
    public async Task Quitar_un_conexo_de_otra_cotizacion_o_inexistente_da_CONEXO_NO_ENCONTRADO()
    {
        var (c, _, _, _) = Preparar(EstadoCotizacion.Entregada);
        var (_, _, _, ajeno) = Preparar(EstadoCotizacion.Entregada);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, ajeno.Id)));

        Assert.Equal("CONEXO_NO_ENCONTRADO", ex.Code);
        Assert.Single(c.Conexos);
    }

    // ----------------------------------------------------- marca de modificación tras entrega

    [Fact]
    public async Task Cambiar_una_Entregada_llena_ModificadaTrasEntregaAt_y_deja_Estado_y_EntregadaAt_iguales()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Entregada);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        Assert.NotNull(c.ModificadaTrasEntregaAt);
        Assert.Equal(EstadoCotizacion.Entregada, c.Estado);
        Assert.Equal(EntregadaEn, c.EntregadaAt);
    }

    [Fact]
    public async Task Cambiar_un_Borrador_no_marca_modificacion_tras_entrega()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Borrador);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        Assert.Null(c.ModificadaTrasEntregaAt);
    }

    // ----------------------------------------------------- aviso al creador

    [Fact]
    public async Task Agregar_tras_entrega_por_otro_usuario_publica_el_aviso_con_servicio_cantidad_y_nuevo_total()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Entregada);
        clsConexosCotizacionCambiados? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()))
            .Callback<clsConexosCotizacionCambiados, CancellationToken>((m, _) => publicado = m).Returns(Task.CompletedTask);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        Assert.NotNull(publicado);
        Assert.Equal("agregado", publicado!.Accion);
        Assert.Equal(c.Id, publicado.CotizacionId);
        Assert.Equal(_actor, publicado.ModificadoPor);
        var item = Assert.Single(publicado.Items);
        Assert.Equal(("Cargue", 3m, 150_000m), (item.Servicio, item.Cantidad, item.Valor));
        Assert.Equal(1_250_000m, publicado.NuevoTotal); // 1.000.000 flete + 100.000 + 150.000
    }

    [Fact]
    public async Task Quitar_tras_entrega_por_otro_usuario_publica_el_aviso_con_el_valor_que_tenia()
    {
        var (c, _, _, existente) = Preparar(EstadoCotizacion.Entregada);
        clsConexosCotizacionCambiados? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()))
            .Callback<clsConexosCotizacionCambiados, CancellationToken>((m, _) => publicado = m).Returns(Task.CompletedTask);

        await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id));

        Assert.NotNull(publicado);
        Assert.Equal("quitado", publicado!.Accion);
        var item = Assert.Single(publicado.Items);
        Assert.Equal(("Cargue", 2m, 100_000m), (item.Servicio, item.Cantidad, item.Valor));
        Assert.Equal(1_000_000m, publicado.NuevoTotal);
    }

    [Fact]
    public async Task Si_quien_cambia_es_el_creador_de_la_solicitud_no_se_avisa_pero_la_marca_si_queda()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Entregada, creadorSolicitud: _actor);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(c.ModificadaTrasEntregaAt);
    }

    [Fact]
    public async Task Sobre_un_Borrador_nunca_se_avisa_aunque_el_actor_no_sea_el_creador()
    {
        var (c, lineaId, servicio, existente) = Preparar(EstadoCotizacion.Borrador);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));
        await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task El_aviso_se_publica_antes_de_guardar_para_viajar_en_la_misma_transaccion()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Entregada);
        var orden = new List<string>();
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task Un_fallo_al_publicar_no_se_traga()
    {
        var (c, lineaId, servicio, _) = Preparar(EstadoCotizacion.Entregada);
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsConexosCotizacionCambiados>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus caído"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Agregar().Handle(ComandoAgregar(c, lineaId, servicio)));

        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----------------------------------------------------- auditoría

    [Fact]
    public async Task La_auditoria_de_agregar_y_quitar_lleva_servicio_cantidad_valor_y_si_fue_tras_la_entrega()
    {
        var (c, lineaId, servicio, existente) = Preparar(EstadoCotizacion.Entregada);

        await Agregar().Handle(ComandoAgregar(c, lineaId, servicio));
        await Quitar().Handle(new clsEliminarConexoCotizacionCommand(c.Id, existente.Id));

        _auditoria.Verify(a => a.Registrar(_actor, "cotizaciones", c.Id, "agregar_conexos",
            It.Is<string>(d => d.Contains("\"servicio\":\"Cargue\"") && d.Contains("\"cantidad\":3") && d.Contains("\"valor\":150000") && d.Contains("\"trasEntrega\":true"))), Times.Once);
        _auditoria.Verify(a => a.Registrar(_actor, "cotizaciones", c.Id, "quitar_conexo",
            It.Is<string>(d => d.Contains("\"valor\":100000") && d.Contains("\"trasEntrega\":true"))), Times.Once);
    }
}
