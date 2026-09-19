using CabinaFletes.Application.CasosDeUso.Cotizaciones;
using CabinaFletes.Application.CasosDeUso.Cotizaciones.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.BibliotecaTarifas.Entidades;
using CabinaFletes.Domain.Cotizaciones.Entidades;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Solicitudes.Entidades;
using Moq;

namespace CabinaFletes.Application.Tests.Cotizaciones;

public class MarcarResultadoCommandTests
{
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<ISolicitudesPort> _solicitudes = new();
    private readonly Mock<ITarifasHistoricasPort> _tarifasHistoricas = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private MarcarResultadoCommandHandler Handler() => new(
        _cotizaciones.Object, _solicitudes.Object, _tarifasHistoricas.Object,
        _auditoria.Object, _usuarioActual.Object, _clock, _publicador.Object, _uow.Object);

    private static Cotizacion Entregada(Guid solicitudId, ResultadoCotizacion resultado = ResultadoCotizacion.SinDefinir) => new()
    {
        SolicitudId = solicitudId,
        Estado = EstadoCotizacion.Entregada,
        Resultado = resultado,
        Lineas = [],
    };

    [Fact]
    public async Task Resultado_no_reconocido_lanza_ValidationFailedException()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new MarcarResultadoCommand(Guid.NewGuid(), "empatada", null, null)));
    }

    [Fact]
    public async Task Cotizacion_inexistente_lanza_NotFoundException()
    {
        _cotizaciones.Setup(c => c.ObtenerParaMarcarResultadoAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Cotizacion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler().Handle(new MarcarResultadoCommand(Guid.NewGuid(), "ganada", null, null)));
    }

    [Fact]
    public async Task Remarcar_el_mismo_resultado_es_idempotente_y_no_guarda_de_nuevo()
    {
        var cotizacionId = Guid.NewGuid();
        var c = Entregada(Guid.NewGuid(), ResultadoCotizacion.Ganada);
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);

        await Handler().Handle(new MarcarResultadoCommand(cotizacionId, "ganada", null, null));

        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
        _tarifasHistoricas.Verify(t => t.Agregar(It.IsAny<TarifaHistorica>()), Times.Never);
    }

    [Fact]
    public async Task Cambiar_de_ganada_a_perdida_lanza_ConflictException()
    {
        var cotizacionId = Guid.NewGuid();
        var c = Entregada(Guid.NewGuid(), ResultadoCotizacion.Ganada);
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Handler().Handle(new MarcarResultadoCommand(cotizacionId, "perdida", "precio", null)));
    }

    /// <summary>
    /// Issue #171: ANTES, una cotización en Borrador con líneas SÍ podía marcar
    /// resultado directo (sin pasar por /entregar), registrando una "entrega
    /// implícita" que ponía EntregadaAt sin que nadie hubiera entregado de verdad -
    /// contaminaba entregadaAt/SLA/cotizaciones emitidas en el tablero de KPIs del
    /// piloto. Ahora SIEMPRE exige Estado == Entregada, sin importar si tiene líneas.
    /// </summary>
    [Fact]
    public async Task Borrador_sin_lineas_no_puede_marcar_resultado()
    {
        var cotizacionId = Guid.NewGuid();
        var c = new Cotizacion { SolicitudId = Guid.NewGuid(), Estado = EstadoCotizacion.Borrador, Lineas = [] };
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler().Handle(new MarcarResultadoCommand(cotizacionId, "ganada", null, null)));
        Assert.Equal("COTIZACION_NO_ENTREGADA", ex.Code);
    }

    [Fact]
    public async Task Borrador_con_lineas_tampoco_puede_marcar_resultado_ni_marca_entregadaAt()
    {
        var cotizacionId = Guid.NewGuid();
        var c = new Cotizacion
        {
            SolicitudId = Guid.NewGuid(), Estado = EstadoCotizacion.Borrador,
            Lineas = [new CotizacionLinea { ValorFlete = 100_000m }],
        };
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Handler().Handle(new MarcarResultadoCommand(cotizacionId, "ganada", null, null)));

        Assert.Equal("COTIZACION_NO_ENTREGADA", ex.Code);
        Assert.Null(c.EntregadaAt);
        Assert.Equal(EstadoCotizacion.Borrador, c.Estado);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Nueva_ganada_realimenta_la_biblioteca_una_sola_vez_por_linea_con_LineaSolicitud()
    {
        var cotizacionId = Guid.NewGuid();
        var lineaSolicitud = new LineaSolicitud { RutaId = Guid.NewGuid(), ConfigVehiculoId = Guid.NewGuid(), Tipologia = TipologiaCarga.CargaSeca };
        var c = Entregada(Guid.NewGuid());
        // El handler consulta ExistenParaCotizacionAsync por c.Id, no por el id del
        // comando: sin igualarlos el mock nunca coincide y devuelve false por defecto.
        c.Id = cotizacionId;
        c.Lineas =
        [
            new CotizacionLinea { ValorFlete = 100_000m, LineaSolicitud = lineaSolicitud },
            new CotizacionLinea { ValorFlete = 50_000m, LineaSolicitud = null }, // sin línea de solicitud: no realimenta
        ];
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(c.SolicitudId, default)).ReturnsAsync((Solicitud?)null);
        _tarifasHistoricas.Setup(t => t.ExistenParaCotizacionAsync(c.Id, default)).ReturnsAsync(false);

        await Handler().Handle(new MarcarResultadoCommand(cotizacionId, "ganada", null, null));

        _tarifasHistoricas.Verify(t => t.Agregar(It.Is<TarifaHistorica>(h => h.Valor == 100_000m)), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Nueva_ganada_no_duplica_si_ya_existen_tarifas_para_la_cotizacion()
    {
        var cotizacionId = Guid.NewGuid();
        var c = Entregada(Guid.NewGuid());
        c.Id = cotizacionId;
        c.Lineas = [new CotizacionLinea { ValorFlete = 100_000m, LineaSolicitud = new LineaSolicitud() }];
        _cotizaciones.Setup(r => r.ObtenerParaMarcarResultadoAsync(cotizacionId, default)).ReturnsAsync(c);
        _solicitudes.Setup(s => s.ObtenerConClienteAsync(c.SolicitudId, default)).ReturnsAsync((Solicitud?)null);
        _tarifasHistoricas.Setup(t => t.ExistenParaCotizacionAsync(c.Id, default)).ReturnsAsync(true);

        await Handler().Handle(new MarcarResultadoCommand(cotizacionId, "ganada", null, null));

        _tarifasHistoricas.Verify(t => t.Agregar(It.IsAny<TarifaHistorica>()), Times.Never);
    }
}
