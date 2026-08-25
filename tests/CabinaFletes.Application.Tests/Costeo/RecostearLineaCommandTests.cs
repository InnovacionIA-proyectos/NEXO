using CabinaFletes.Application.CasosDeUso.Costeo;
using CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Costeo.Reglas;
using CabinaFletes.Domain.Enums;
using Moq;

namespace CabinaFletes.Application.Tests.Costeo;

/// <summary>
/// Recostear una línea que estaba esperando una tarifa.
///
/// Lo que se prueba es sobre todo lo que NO debe pasar: que un recosteo que sigue sin
/// tarifa no borre el pendiente ni marque la línea como resuelta. Un recosteo optimista
/// que cierra el pendiente sin haber resuelto nada deja la línea invisible: ya no
/// aparece en la cola del Administrador de Tarifas y nunca se va a costear.
/// </summary>
public class RecostearLineaCommandTests
{
    private readonly Mock<IPropuestaDeLineaPort> _propuesta = new();
    private readonly Mock<IPendientesDeCosteoPort> _pendientes = new();
    private readonly Mock<ILineasCosteadasPort> _lineas = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid Trabajo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Solicitud = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Linea = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private RecostearLineaCommandHandler Handler() =>
        new(_propuesta.Object, _pendientes.Object, _lineas.Object, _uow.Object);

    private void CuandoLaPropuestaEs(ResultadoDeCosteo r) =>
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(r);

    [Fact]
    public async Task Si_ya_hay_tarifa_la_linea_se_resuelve_y_el_pendiente_se_cierra()
    {
        CuandoLaPropuestaEs(new ResultadoDeCosteo(1_100_000m, "plaza", "Ya hay tarifa de plaza vigente."));

        await Handler().Handle(new RecostearLineaCommand(Trabajo, Solicitud, Linea));

        _lineas.Verify(l => l.MarcarAutoResueltaAsync(Linea, 1_100_000m, "plaza", It.IsAny<CancellationToken>()), Times.Once);
        _pendientes.Verify(p => p.MarcarResueltoAsync(
            Trabajo, Linea, TipoPendienteDeCosteo.Manual, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_sigue_sin_tarifa_el_pendiente_se_queda_donde_estaba()
    {
        CuandoLaPropuestaEs(new ResultadoDeCosteo(null, null, "Sigue sin tarifa para la ruta."));

        await Handler().Handle(new RecostearLineaCommand(Trabajo, Solicitud, Linea));

        // Lo importante: no se cierra el pendiente. Cerrarlo dejaría la línea invisible
        // en la cola del Administrador de Tarifas y no se costearía nunca.
        _pendientes.Verify(p => p.MarcarResueltoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_sigue_sin_tarifa_no_se_escribe_nada()
    {
        CuandoLaPropuestaEs(new ResultadoDeCosteo(null, null, "Sigue sin tarifa."));

        await Handler().Handle(new RecostearLineaCommand(Trabajo, Solicitud, Linea));

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
            Handler().Handle(new RecostearLineaCommand(Trabajo, Solicitud, Linea)));

        _pendientes.Verify(p => p.MarcarResueltoAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
