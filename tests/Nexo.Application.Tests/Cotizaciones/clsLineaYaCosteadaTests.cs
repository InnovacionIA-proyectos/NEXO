using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.Common;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Dtos;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Cotizaciones;

/// <summary>
/// Una línea de solicitud tiene un solo flete por cotización: agregar otro responde 409 LINEA_YA_COSTEADA en vez de duplicar la línea e
/// inflar el total, también cuando dos pedidos llegan a la vez y es la base la que lo detecta.
/// </summary>
public class clsLineaYaCosteadaTests
{
    private static readonly Exception Choque = new InvalidOperationException("índice único");

    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ILineasSolicitudPort> _lineasSolicitud = new();
    private readonly Mock<ITarifasHistoricasPort> _historicas = new();
    private readonly Mock<IPropuestaTarifaPort> _propuestas = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsAgregarLineaCotizacionCommandHandler Agregar() => new(
        _cotizaciones.Object, _lineasSolicitud.Object, _historicas.Object, _propuestas.Object,
        _auditoria.Object, _usuario.Object, TimeProvider.System, _uow.Object);

    private clsAplicarPropuestasCommandHandler Aplicar() => new(
        _cotizaciones.Object, _lineasSolicitud.Object, _propuestas.Object, _solicitudes.Object,
        _auditoria.Object, _usuario.Object, _uow.Object);

    private static clsCotizacionLinea Flete(Guid? lineaId) => new() { LineaSolicitudId = lineaId, ValorFlete = 1_000_000m, Fuente = FuenteFlete.Manual };

    private (clsCotizacion Cotizacion, Guid SolicitudId) CotizacionConLineas(params Guid?[] lineasConFlete)
    {
        var solicitudId = Guid.NewGuid();
        var cotizacion = new clsCotizacion { SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        foreach (var l in lineasConFlete) cotizacion.Lineas.Add(Flete(l));
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(cotizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(cotizacion);
        return (cotizacion, solicitudId);
    }

    private clsLineaSolicitud LineaDeLaSolicitud(Guid solicitudId, Guid lineaId)
    {
        var linea = new clsLineaSolicitud { Id = lineaId, SolicitudId = solicitudId, EstadoLinea = EstadoLinea.PorCostear };
        _lineasSolicitud.Setup(l => l.ObtenerDeSolicitudAsync(lineaId, solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(linea);
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPropuestaSolicitudDto(solicitudId, [], []));
        return linea;
    }

    // ------------------------------------------------ agregar

    [Fact]
    public async Task Agregar_el_flete_de_una_linea_que_ya_lo_tiene_es_409_y_no_cambia_nada()
    {
        var lineaId = Guid.NewGuid();
        var (cotizacion, solicitudId) = CotizacionConLineas(lineaId);
        var lineaSol = LineaDeLaSolicitud(solicitudId, lineaId);

        var ex = await Assert.ThrowsAsync<clsConflictException>(
            () => Agregar().Handle(new clsAgregarLineaCotizacionCommand(cotizacion.Id, lineaId, 2_000_000m, null, "Manual")));

        Assert.Equal("LINEA_YA_COSTEADA", ex.Code);
        Assert.Single(cotizacion.Lineas);
        Assert.Equal(EstadoLinea.PorCostear, lineaSol.EstadoLinea);
        _propuestas.Verify(p => p.ProponerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agregar_el_flete_de_otra_linea_de_la_misma_cotizacion_funciona()
    {
        var yaCosteada = Guid.NewGuid();
        var nueva = Guid.NewGuid();
        var (cotizacion, solicitudId) = CotizacionConLineas(yaCosteada);
        LineaDeLaSolicitud(solicitudId, nueva);

        var dto = await Agregar().Handle(new clsAgregarLineaCotizacionCommand(cotizacion.Id, nueva, 2_000_000m, null, "Manual"));

        Assert.Equal(2, dto.Lineas.Count);
    }

    [Fact]
    public async Task Los_fletes_sin_linea_de_solicitud_no_cuentan_como_repetidos()
    {
        var (cotizacion, _) = CotizacionConLineas((Guid?)null);

        var dto = await Agregar().Handle(new clsAgregarLineaCotizacionCommand(cotizacion.Id, null, 500_000m, null, "Manual"));

        Assert.Equal(2, dto.Lineas.Count);
    }

    [Fact]
    public async Task Si_la_base_detecta_el_flete_repetido_al_guardar_es_409_y_descarta_lo_pendiente()
    {
        var lineaId = Guid.NewGuid();
        var (cotizacion, solicitudId) = CotizacionConLineas();
        LineaDeLaSolicitud(solicitudId, lineaId);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(Choque);
        _cotizaciones.Setup(c => c.EsChoqueDeLineaDuplicada(Choque)).Returns(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(
            () => Agregar().Handle(new clsAgregarLineaCotizacionCommand(cotizacion.Id, lineaId, 2_000_000m, null, "Manual")));

        Assert.Equal("LINEA_YA_COSTEADA", ex.Code);
        _cotizaciones.Verify(c => c.DescartarCambiosPendientes(), Times.Once);
    }

    [Fact]
    public async Task Un_error_de_guardado_que_no_es_el_del_flete_repetido_no_se_traduce()
    {
        var lineaId = Guid.NewGuid();
        var (cotizacion, solicitudId) = CotizacionConLineas();
        LineaDeLaSolicitud(solicitudId, lineaId);
        var otro = new InvalidOperationException("otro");
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(otro);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Agregar().Handle(new clsAgregarLineaCotizacionCommand(cotizacion.Id, lineaId, 2_000_000m, null, "Manual")));

        Assert.Same(otro, ex);
        _cotizaciones.Verify(c => c.DescartarCambiosPendientes(), Times.Never);
    }

    // ------------------------------------------------ aplicar propuestas

    private void Propuesta(Guid solicitudId, params Guid[] lineas) =>
        _propuestas.Setup(p => p.ProponerAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsPropuestaSolicitudDto(solicitudId,
                lineas.Select(l => new clsPropuestaLineaDto(l, "A → B", "C2", 1000, null, null, null, 1_000_000m, "plaza", "Plaza vigente.")).ToList(), []));

    [Fact]
    public async Task Aplicar_propuestas_con_un_choque_de_otro_pedido_vuelve_a_leer_y_responde_sin_agregar_nada()
    {
        var uno = Guid.NewGuid();
        var dos = Guid.NewGuid();
        var (vacia, solicitudId) = CotizacionConLineas();
        // Con el choque ya está todo guardado: la segunda lectura trae las dos líneas con su flete.
        var yaAplicada = new clsCotizacion { Id = vacia.Id, SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        yaAplicada.Lineas.Add(Flete(uno));
        yaAplicada.Lineas.Add(Flete(dos));
        _cotizaciones.SetupSequence(c => c.ObtenerConLineasYConexosAsync(vacia.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacia).ReturnsAsync(yaAplicada);
        _solicitudes.Setup(s => s.ContarLineasAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _lineasSolicitud.Setup(l => l.ObtenerPorSolicitudAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, clsLineaSolicitud>)new Dictionary<Guid, clsLineaSolicitud>());
        Propuesta(solicitudId, uno, dos);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(Choque);
        _cotizaciones.Setup(c => c.EsChoqueDeLineaDuplicada(Choque)).Returns(true);

        var resultado = await Aplicar().Handle(new clsAplicarPropuestasCommand(vacia.Id));

        Assert.Equal(0, resultado.Agregadas);
        Assert.Equal(2, resultado.Cotizacion.Lineas.Count);
        _cotizaciones.Verify(c => c.DescartarCambiosPendientes(), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Aplicar_propuestas_con_un_choque_que_se_repite_es_409()
    {
        var uno = Guid.NewGuid();
        var (vacia, solicitudId) = CotizacionConLineas();
        var otraLectura = new clsCotizacion { Id = vacia.Id, SolicitudId = solicitudId, Estado = EstadoCotizacion.Borrador };
        _cotizaciones.SetupSequence(c => c.ObtenerConLineasYConexosAsync(vacia.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vacia).ReturnsAsync(otraLectura);
        _solicitudes.Setup(s => s.ContarLineasAsync(solicitudId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _lineasSolicitud.Setup(l => l.ObtenerPorSolicitudAsync(solicitudId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, clsLineaSolicitud>)new Dictionary<Guid, clsLineaSolicitud>());
        Propuesta(solicitudId, uno);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(Choque);
        _cotizaciones.Setup(c => c.EsChoqueDeLineaDuplicada(Choque)).Returns(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Aplicar().Handle(new clsAplicarPropuestasCommand(vacia.Id)));

        Assert.Equal("LINEA_YA_COSTEADA", ex.Code);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
