using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Nexo.Mensajes.Costeo;
using Moq;

namespace Nexo.Application.Tests.Costeo;

/// <summary>
/// Costear una línea tiene tres desenlaces y cada uno lleva a un sitio distinto.
///
/// Estas pruebas fijan cuál es cuál, porque confundirlos tiene consecuencias reales:
/// tratar una línea sin tarifa como si fuera un fallo la manda a reintentos que nunca
/// van a resolver nada, y al agotarlos dispara la alerta del equipo por algo que no
/// está roto.
///
/// El fallo técnico NO se maneja aquí: la excepción sube y MassTransit reintenta. Solo
/// cuando los reintentos se agotan se convierte en un desenlace, y eso ocurre en el
/// consumidor de fallos.
///
/// S5-HU-19.2/#282: el desenlace ya no depende de si hay valor (RequiereTarifaManual),
/// depende del <c>Modo</c> — ConsultaCompleta deja la línea pendiente AUNQUE haya un
/// candidato con valor; SoloSicetac/PlazaVigenteMayor auto-resuelven Y agregan el flete
/// a la cotización auto-costeada.
/// </summary>
public class clsCostearLineaCommandTests
{
    private readonly Mock<IPropuestaDeLineaPort> _propuesta = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IPendientesDeCosteoPort> _pendientes = new();
    private readonly Mock<ILineasCosteadasPort> _lineas = new();
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid Trabajo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid clsSolicitud = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Linea = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    public clsCostearLineaCommandTests()
    {
        // Por defecto, sin cotización auto-costeada previa — el handler crea una nueva.
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<clsCotizacion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsCotizacion?)null);
    }

    private clsCostearLineaCommandHandler Handler() => new(
        _propuesta.Object, _publicador.Object, _pendientes.Object, _lineas.Object, _cotizaciones.Object, _uow.Object);

    private static clsCostearLineaCommand Comando() => new(Trabajo, clsSolicitud, Linea);

    private void CuandoLaPropuestaEs(clsDecisionDeFlujo decision) =>
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(decision);

    private static clsDecisionDeFlujo Automatica(decimal valor, string fuente, string justificacion, ModoDecision modo = ModoDecision.SoloSicetac) =>
        new(new clsResultadoDeCosteo(valor, fuente, justificacion), modo);

    private static clsDecisionDeFlujo ConsultaCompleta(decimal? valor, string? fuente, string justificacion) =>
        new(new clsResultadoDeCosteo(valor, fuente, justificacion), ModoDecision.ConsultaCompleta);

    [Fact]
    public async Task Con_propuesta_automatica_publica_LineaCosteada_con_su_fuente()
    {
        CuandoLaPropuestaEs(Automatica(1_500_000m, "sicetac", "El piso SICE-TAC gobierna."));

        clsLineaCosteada? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsLineaCosteada>(), It.IsAny<CancellationToken>()))
            .Callback<clsLineaCosteada, CancellationToken>((m, _) => publicado = m)
            .Returns(Task.CompletedTask);

        await Handler().Handle(Comando());

        Assert.NotNull(publicado);
        Assert.Equal(Trabajo, publicado!.TrabajoId);
        Assert.Equal(Linea, publicado.LineaId);
        Assert.Equal(1_500_000m, publicado.ValorPropuesto);
        Assert.Equal("sicetac", publicado.Fuente);
    }

    [Fact]
    public async Task Con_propuesta_automatica_la_linea_queda_marcada_como_resuelta_por_el_servicio()
    {
        CuandoLaPropuestaEs(Automatica(900_000m, "plaza", "Solo plaza.", ModoDecision.PlazaVigenteMayor));

        await Handler().Handle(Comando());

        _lineas.Verify(l => l.MarcarAutoResueltaAsync(Linea, 900_000m, "plaza", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Con_propuesta_automatica_agrega_el_flete_a_una_cotizacion_auto_costeada_nueva()
    {
        CuandoLaPropuestaEs(Automatica(900_000m, "plaza", "Solo plaza.", ModoDecision.PlazaVigenteMayor));

        clsCotizacion? agregada = null;
        _cotizaciones.Setup(c => c.Agregar(It.IsAny<clsCotizacion>())).Callback<clsCotizacion>(c => agregada = c);

        await Handler().Handle(Comando());

        Assert.NotNull(agregada);
        Assert.True(agregada!.EsAutoCosteo);
        Assert.Equal(clsSolicitud, agregada.SolicitudId);
        var linea = Assert.Single(agregada.Lineas);
        Assert.Equal(Linea, linea.LineaSolicitudId);
        Assert.Equal(900_000m, linea.ValorFlete);
        Assert.Equal(FuenteFlete.Tarifario, linea.Fuente);
    }

    [Fact]
    public async Task Con_propuesta_automatica_reutiliza_la_cotizacion_auto_costeada_existente_sin_duplicar()
    {
        var existente = new clsCotizacion { SolicitudId = clsSolicitud, Estado = EstadoCotizacion.Borrador, EsAutoCosteo = true };
        existente.Lineas.Add(new clsCotizacionLinea { CotizacionId = existente.Id, LineaSolicitudId = Linea, ValorFlete = 1m, Fuente = FuenteFlete.Manual });
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<clsCotizacion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);
        CuandoLaPropuestaEs(Automatica(900_000m, "plaza", "Solo plaza.", ModoDecision.PlazaVigenteMayor));

        await Handler().Handle(Comando());

        // Ya estaba (recosteo/reintento): no se duplica la línea ni se crea otra cotización.
        Assert.Single(existente.Lineas);
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<clsCotizacion>()), Times.Never);
    }

    [Fact]
    public async Task Con_propuesta_automatica_no_se_registra_ningun_pendiente()
    {
        CuandoLaPropuestaEs(Automatica(900_000m, "plaza", "Solo plaza.", ModoDecision.PlazaVigenteMayor));

        await Handler().Handle(Comando());

        _pendientes.Verify(p => p.RegistrarAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------- requiere una persona (ConsultaCompleta)

    [Fact]
    public async Task ConsultaCompleta_publica_LineaRequiereTarifaManual_con_la_justificacion_como_motivo()
    {
        const string justificacion = "Sin flete de plaza ni piso SICE-TAC para la ruta: requiere atención manual.";
        CuandoLaPropuestaEs(ConsultaCompleta(null, null, justificacion));

        clsLineaRequiereTarifaManual? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsLineaRequiereTarifaManual>(), It.IsAny<CancellationToken>()))
            .Callback<clsLineaRequiereTarifaManual, CancellationToken>((m, _) => publicado = m)
            .Returns(Task.CompletedTask);

        await Handler().Handle(Comando());

        Assert.NotNull(publicado);
        Assert.Equal(clsSolicitud, publicado!.SolicitudId);
        Assert.Equal(justificacion, publicado.Motivo);
    }

    [Fact]
    public async Task ConsultaCompleta_queda_registrada_como_pendiente_de_tipo_Manual_AUNQUE_tenga_valor()
    {
        // S5-HU-19.2/#282: un solo candidato en consulta completa SÍ trae Valor, pero el
        // modo manda — sigue pendiente, no se auto-aplica.
        CuandoLaPropuestaEs(ConsultaCompleta(2_500_000m, "sicetac", "Solo se encontró piso SICE-TAC."));

        await Handler().Handle(Comando());

        _pendientes.Verify(p => p.RegistrarAsync(
            Trabajo, clsSolicitud, Linea, TipoPendienteDeCosteo.Manual, "Solo se encontró piso SICE-TAC.",
            It.IsAny<CancellationToken>()), Times.Once);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsultaCompleta_NO_publica_LineaCosteada_ni_se_marca_resuelta_ni_agrega_cotizacion()
    {
        CuandoLaPropuestaEs(ConsultaCompleta(null, null, "No hay tarifa vigente."));

        await Handler().Handle(Comando());

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsLineaCosteada>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<clsCotizacion>()), Times.Never);
    }

    // ---------------------------------------------- fallo de fuente

    [Fact]
    public async Task Un_fallo_de_la_fuente_se_propaga_para_que_el_transporte_reintente()
    {
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("DwSice no respondió."));

        // No se traga la excepción a propósito: tragarla convertiría un fallo
        // transitorio en un pendiente definitivo sin haber reintentado ni una vez.
        await Assert.ThrowsAsync<TimeoutException>(() => Handler().Handle(Comando()));

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsLineaRequiereTarifaManual>(), It.IsAny<CancellationToken>()), Times.Never);
        _pendientes.Verify(p => p.RegistrarAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // S6-HU-24.3/#320: una línea rechazada a propósito no se costea aunque llegue un mensaje encolado antes del rechazo: ni se le propone
    // un flete ni se publica nada ni se toca el pendiente.
    [Fact]
    public async Task Una_linea_rechazada_no_se_costea()
    {
        _lineas.Setup(l => l.EstaRechazadaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Handler().Handle(Comando());

        _propuesta.Verify(p => p.ProponerParaLineaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarPendienteAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
