using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Cotizaciones.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Costeo.Reglas;
using Nexo.Domain.Cotizaciones.Entidades;
using Nexo.Domain.Enums;
using Moq;

namespace Nexo.Application.Tests.Costeo;

/// <summary>
/// Recostear una línea que estaba esperando una tarifa.
///
/// Lo que se prueba es sobre todo lo que NO debe pasar: que un recosteo que sigue en
/// ConsultaCompleta (S5-HU-19.2/#282 — ya no "sin tarifa", el modo manda, no si hay
/// valor) no borre el pendiente ni marque la línea como resuelta. Un recosteo optimista
/// que cierra el pendiente sin haber resuelto nada deja la línea invisible: ya no
/// aparece en la cola del Administrador de Tarifas y nunca se va a costear.
/// </summary>
public class clsRecostearLineaCommandTests
{
    private readonly Mock<IPropuestaDeLineaPort> _propuesta = new();
    private readonly Mock<IPendientesDeCosteoPort> _pendientes = new();
    private readonly Mock<ILineasCosteadasPort> _lineas = new();
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid Trabajo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid clsSolicitud = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Linea = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    public clsRecostearLineaCommandTests()
    {
        _cotizaciones.Setup(c => c.ObtenerConLineasYConexosAsync(
            It.IsAny<System.Linq.Expressions.Expression<Func<clsCotizacion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsCotizacion?)null);
    }

    private clsRecostearLineaCommandHandler Handler() =>
        new(_propuesta.Object, _pendientes.Object, _lineas.Object, _cotizaciones.Object, _uow.Object);

    private void CuandoLaPropuestaEs(clsDecisionDeFlujo d) =>
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(d);

    private static clsDecisionDeFlujo Automatica(decimal valor, string fuente, string justificacion) =>
        new(new clsResultadoDeCosteo(valor, fuente, justificacion), ModoDecision.PlazaVigenteMayor);

    private static clsDecisionDeFlujo ConsultaCompleta(decimal? valor, string? fuente, string justificacion) =>
        new(new clsResultadoDeCosteo(valor, fuente, justificacion), ModoDecision.ConsultaCompleta);

    [Fact]
    public async Task Si_ahora_resuelve_automatico_la_linea_se_resuelve_y_el_pendiente_se_cierra()
    {
        CuandoLaPropuestaEs(Automatica(1_100_000m, "plaza", "La plaza vigente supera el piso."));

        await Handler().Handle(new clsRecostearLineaCommand(Trabajo, clsSolicitud, Linea));

        _lineas.Verify(l => l.MarcarAutoResueltaAsync(Linea, 1_100_000m, "plaza", It.IsAny<CancellationToken>()), Times.Once);
        _pendientes.Verify(p => p.MarcarResueltoAsync(
            Trabajo, Linea, TipoPendienteDeCosteo.Manual, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_ahora_resuelve_automatico_agrega_el_flete_a_la_cotizacion_auto_costeada()
    {
        CuandoLaPropuestaEs(Automatica(1_100_000m, "plaza", "La plaza vigente supera el piso."));

        clsCotizacion? agregada = null;
        _cotizaciones.Setup(c => c.Agregar(It.IsAny<clsCotizacion>())).Callback<clsCotizacion>(c => agregada = c);

        await Handler().Handle(new clsRecostearLineaCommand(Trabajo, clsSolicitud, Linea));

        Assert.NotNull(agregada);
        var linea = Assert.Single(agregada!.Lineas);
        Assert.Equal(1_100_000m, linea.ValorFlete);
    }

    [Fact]
    public async Task Si_sigue_en_ConsultaCompleta_el_pendiente_se_queda_donde_estaba_AUNQUE_tenga_valor()
    {
        // S5-HU-19.2/#282: un solo candidato en consulta completa SÍ trae Valor, pero el
        // modo manda — sigue pendiente.
        CuandoLaPropuestaEs(ConsultaCompleta(2_500_000m, "sicetac", "Solo se encontró piso SICE-TAC."));

        await Handler().Handle(new clsRecostearLineaCommand(Trabajo, clsSolicitud, Linea));

        // Lo importante: no se cierra el pendiente. Cerrarlo dejaría la línea invisible
        // en la cola del Administrador de Tarifas y no se costearía nunca.
        _pendientes.Verify(p => p.MarcarResueltoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_sigue_en_ConsultaCompleta_no_se_escribe_nada()
    {
        CuandoLaPropuestaEs(ConsultaCompleta(null, null, "Sigue sin tarifa."));

        await Handler().Handle(new clsRecostearLineaCommand(Trabajo, clsSolicitud, Linea));

        // Un recosteo que no resolvió nada no debe generar escrituras: con miles de
        // pendientes revisados en cada carga de tarifas, eso sería ruido en la base.
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_fallo_de_fuente_durante_el_recosteo_se_propaga()
    {
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("DwSice no respondió."));

        // Sube para que el bus reintente. Tragarla dejaría la línea esperando sin que
        // nadie sepa que el recosteo ni siquiera pudo intentarse.
        await Assert.ThrowsAsync<TimeoutException>(() =>
            Handler().Handle(new clsRecostearLineaCommand(Trabajo, clsSolicitud, Linea)));

        _pendientes.Verify(p => p.MarcarResueltoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
