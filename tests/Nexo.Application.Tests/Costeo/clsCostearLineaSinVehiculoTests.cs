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
/// S5-HU-20.5/#297: la línea de Cupo sin vehículo (traída por el cargue masivo / costeo por línea) queda
/// PorCostear con el motivo literal "Línea sin vehículo: requiere consulta completa", sin escribir ningún
/// flete — y ese motivo es el que ven la Mesa y el pendiente, no el texto largo de la justificación.
/// </summary>
public class clsCostearLineaSinVehiculoTests
{
    private static readonly Guid Trabajo = Guid.NewGuid();
    private static readonly Guid Solicitud = Guid.NewGuid();
    private static readonly Guid Linea = Guid.NewGuid();

    private readonly Mock<IPropuestaDeLineaPort> _propuesta = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IPendientesDeCosteoPort> _pendientes = new();
    private readonly Mock<ILineasCosteadasPort> _lineas = new();
    private readonly Mock<ICotizacionRepository> _cotizaciones = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private clsCostearLineaCommandHandler Handler() => new(
        _propuesta.Object, _publicador.Object, _pendientes.Object, _lineas.Object, _cotizaciones.Object, _uow.Object);

    [Fact]
    public async Task Linea_sin_vehiculo_queda_pendiente_con_el_motivo_literal_y_sin_flete()
    {
        var decision = clsReglaDeCosteo.DecidirFlujo(
            false, new clsCandidatoDeTarifa("sicetac", 2_500_000m), new clsCandidatoDeTarifa("plaza", 3_000_000m), null, null,
            lineaSinVehiculoQueLoExige: true);
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(decision);
        clsLineaRequiereTarifaManual? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsLineaRequiereTarifaManual>(), It.IsAny<CancellationToken>()))
            .Callback<clsLineaRequiereTarifaManual, CancellationToken>((m, _) => publicado = m).Returns(Task.CompletedTask);

        await Handler().Handle(new clsCostearLineaCommand(Trabajo, Solicitud, Linea));

        const string Motivo = "Línea sin vehículo: requiere consulta completa";
        _lineas.Verify(l => l.MarcarPendienteAsync(Linea, Motivo, It.IsAny<CancellationToken>()), Times.Once);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _pendientes.Verify(p => p.RegistrarAsync(Trabajo, Solicitud, Linea, It.IsAny<TipoPendienteDeCosteo>(), Motivo, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Motivo, publicado!.Motivo);
        _cotizaciones.Verify(c => c.Agregar(It.IsAny<clsCotizacion>()), Times.Never);
    }

    [Fact]
    public async Task Una_consulta_completa_normal_sigue_usando_la_justificacion_como_motivo()
    {
        var decision = new clsDecisionDeFlujo(new clsResultadoDeCosteo(2_000_000m, "plaza", "Justificación de siempre."), ModoDecision.ConsultaCompleta);
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(decision);

        await Handler().Handle(new clsCostearLineaCommand(Trabajo, Solicitud, Linea));

        _lineas.Verify(l => l.MarcarPendienteAsync(Linea, "Justificación de siempre.", It.IsAny<CancellationToken>()), Times.Once);
    }
}
