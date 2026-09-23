using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.CasosDeUso.Costeo.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.Costeo.Reglas;
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
/// </summary>
public class clsCostearLineaCommandTests
{
    private readonly Mock<IPropuestaDeLineaPort> _propuesta = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IPendientesDeCosteoPort> _pendientes = new();
    private readonly Mock<ILineasCosteadasPort> _lineas = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private static readonly Guid Trabajo = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid clsSolicitud = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid Linea = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    private clsCostearLineaCommandHandler Handler() => new(
        _propuesta.Object, _publicador.Object, _pendientes.Object, _lineas.Object, _uow.Object);

    private static clsCostearLineaCommand Comando() => new(Trabajo, clsSolicitud, Linea);

    private void CuandoLaPropuestaEs(clsResultadoDeCosteo resultado) =>
        _propuesta.Setup(p => p.ProponerParaLineaAsync(Linea, It.IsAny<CancellationToken>())).ReturnsAsync(resultado);

    [Fact]
    public async Task Con_propuesta_publica_LineaCosteada_con_su_fuente()
    {
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(1_500_000m, "sicetac", "El piso SICE-TAC gobierna."));

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
    public async Task Con_propuesta_la_linea_queda_marcada_como_resuelta_por_el_servicio()
    {
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(900_000m, "plaza", "Solo plaza."));

        await Handler().Handle(Comando());

        _lineas.Verify(l => l.MarcarAutoResueltaAsync(Linea, 900_000m, "plaza", It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Con_propuesta_no_se_registra_ningun_pendiente()
    {
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(900_000m, "plaza", "Solo plaza."));

        await Handler().Handle(Comando());

        _pendientes.Verify(p => p.RegistrarAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<TipoPendienteDeCosteo>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------------------------- requiere una persona

    [Fact]
    public async Task Sin_tarifa_publica_LineaRequiereTarifaManual_con_la_justificacion_como_motivo()
    {
        const string justificacion = "Sin flete de plaza ni piso SICE-TAC para la ruta: requiere atención manual.";
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(null, null, justificacion));

        clsLineaRequiereTarifaManual? publicado = null;
        _publicador.Setup(p => p.PublicarAsync(It.IsAny<clsLineaRequiereTarifaManual>(), It.IsAny<CancellationToken>()))
            .Callback<clsLineaRequiereTarifaManual, CancellationToken>((m, _) => publicado = m)
            .Returns(Task.CompletedTask);

        await Handler().Handle(Comando());

        Assert.NotNull(publicado);
        Assert.Equal(clsSolicitud, publicado!.SolicitudId);
        // El motivo que ve el Administrador de Tarifas es la justificación de la regla,
        // no un texto genérico: es lo que le dice qué falta.
        Assert.Equal(justificacion, publicado.Motivo);
    }

    [Fact]
    public async Task Sin_tarifa_queda_registrado_como_pendiente_de_tipo_Manual()
    {
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(null, null, "No hay tarifa vigente."));

        await Handler().Handle(Comando());

        _pendientes.Verify(p => p.RegistrarAsync(
            Trabajo, clsSolicitud, Linea, TipoPendienteDeCosteo.Manual, "No hay tarifa vigente.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_tarifa_NO_se_publica_LineaCosteada_ni_se_marca_resuelta()
    {
        CuandoLaPropuestaEs(new clsResultadoDeCosteo(null, null, "No hay tarifa vigente."));

        await Handler().Handle(Comando());

        _publicador.Verify(p => p.PublicarAsync(It.IsAny<clsLineaCosteada>(), It.IsAny<CancellationToken>()), Times.Never);
        _lineas.Verify(l => l.MarcarAutoResueltaAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
}
