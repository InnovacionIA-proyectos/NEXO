using CabinaFletes.Application.CasosDeUso.Priorizacion;
using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Mensajes.Priorizacion;
using Moq;

namespace CabinaFletes.Application.Tests.Priorizacion;

/// <summary>
/// Recalibrar los pesos del score. El endpoint no existía: la entidad se decía
/// "editable por el Admin" y no había forma de editarla, así que el segundo disparador
/// que pide la HU no tenía quién lo disparara.
///
/// Lo que se protege: que recalibrar no recalcule dentro de la petición, que el aviso
/// salga en la misma transacción que el cambio, y que una configuración inválida no
/// llegue a tocar la fila.
/// </summary>
public class ActualizarParametrosScoreCommandTests
{
    private readonly Mock<IParametrosScorePort> _parametros = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    private readonly ParametrosScore _fila = new();

    public ActualizarParametrosScoreCommandTests() =>
        _parametros.Setup(p => p.ParaEditarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_fila);

    private ActualizarParametrosScoreCommandHandler Handler() =>
        new(_parametros.Object, _publicador.Object, Reloj, _uow.Object);

    private static ParametrosScoreDto Validos(
        double urgencia = 0.50, double valor = 0.20, double prob = 0.20, double tipo = 0.10) =>
        new(urgencia, valor, prob, tipo,
            HorizonteHoras: 240, ValorReferencia: 100_000_000m,
            PuntajeLicitacion: 100, PuntajeSpot: 50,
            ValorPorDefectoSinValor: 30, ProbabilidadPorDefecto: 50);

    [Fact]
    public async Task Guarda_los_pesos_nuevos_sobre_la_fila_existente()
    {
        var r = await Handler().Handle(new ActualizarParametrosScoreCommand(Validos(), Guid.NewGuid()));

        Assert.Equal(0.50, _fila.PesoUrgencia);
        Assert.Equal(0.50, r.PesoUrgencia);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Publica_el_aviso_para_que_el_recalculo_ocurra_aparte()
    {
        // Recalcular todas las activas dentro de la petición dejaría al Admin esperando
        // por algo que no necesita esperar, y un fallo del recálculo le devolvería un
        // error por un cambio que sí se guardó.
        var usuario = Guid.NewGuid();

        await Handler().Handle(new ActualizarParametrosScoreCommand(Validos(), usuario));

        _publicador.Verify(p => p.PublicarAsync(
            It.Is<ParametrosDePriorizacionCambiaron>(m =>
                m.CadenaId == CadenaDePriorizacion.Unica && m.CambiadoPor == usuario),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_aviso_se_publica_antes_de_guardar_para_que_viaje_en_la_misma_transaccion()
    {
        // Con el Outbox, publicar ANTES de SaveChanges es lo que mete el mensaje en la
        // misma transacción. Al revés quedaría fuera, y podría haber un parámetro
        // guardado sin recálculo, o un recálculo por un cambio que se revirtió.
        var orden = new List<string>();
        _publicador
            .Setup(p => p.PublicarAsync(It.IsAny<ParametrosDePriorizacionCambiaron>(), It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("publicar")).Returns(Task.CompletedTask);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => orden.Add("guardar")).ReturnsAsync(1);

        await Handler().Handle(new ActualizarParametrosScoreCommand(Validos(), Guid.NewGuid()));

        Assert.Equal(new[] { "publicar", "guardar" }, orden);
    }

    [Fact]
    public async Task Pesos_que_no_suman_uno_no_llegan_a_tocar_la_fila()
    {
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new ActualizarParametrosScoreCommand(
                Validos(urgencia: 0.90, valor: 0.90, prob: 0.90, tipo: 0.90), Guid.NewGuid())));

        // La fila queda intacta: se valida sobre una copia, así que un rechazo no puede
        // dejarla a medio modificar.
        Assert.Equal(0.40, _fila.PesoUrgencia);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publicador.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Una_configuracion_invalida_no_dispara_recalculo()
    {
        // Sería lo peor de los dos mundos: el cambio se rechaza y el recálculo corre
        // igual, gastando una pasada completa sobre las activas por nada.
        await Assert.ThrowsAsync<ValidationFailedException>(() =>
            Handler().Handle(new ActualizarParametrosScoreCommand(
                Validos() with { HorizonteHoras = 0 }, Guid.NewGuid())));

        _publicador.VerifyNoOtherCalls();
    }
}
