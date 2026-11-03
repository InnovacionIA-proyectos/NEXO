using Nexo.Application.CasosDeUso.Priorizacion;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Enums;
using Nexo.Domain.Priorizacion.Entidades;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;
using Moq;

namespace Nexo.Application.Tests.Priorizacion;

/// <summary>
/// El refresco del score persistido, que antes vivía en un PeriodicTimer del API.
///
/// Lo que se protege aquí no es el cálculo —ese es Domain y ya está probado— sino cómo
/// se recorre: por lotes, sin escribir lo que no cambió, y con los parámetros leídos una
/// sola vez para toda la corrida.
/// </summary>
public class clsRecalcularScoresActivosCommandTests
{
    private readonly Mock<ISolicitudesParaRecalcularPort> _activas = new();
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly TimeProvider Reloj = TimeProvider.System;

    public clsRecalcularScoresActivosCommandTests() =>
        _solicitudes.Setup(s => s.ParametrosScoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsParametrosScore());

    private clsRecalcularScoresActivosCommandHandler Handler() =>
        new(_activas.Object, _solicitudes.Object, Reloj, _uow.Object);

    private static clsSolicitud Activa(decimal scoreGuardado, TipoSolicitud tipo = TipoSolicitud.Licitacion) => new()
    {
        Id = Guid.NewGuid(),
        Tipo = tipo,
        Estado = EstadoSolicitud.Pendiente,
        Deadline = DateTimeOffset.UtcNow.AddDays(2),
        ValorEstimado = 50_000_000m,
        ProbabilidadCierre = 70m,
        ScorePrioridad = scoreGuardado
    };

    /// <summary>Devuelve los lotes en orden y luego se queda vacío, como haría la base.</summary>
    private void ResponderConLotes(params IReadOnlyList<clsSolicitud>[] lotes)
    {
        var i = 0;
        _activas.Setup(a => a.ActivasAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => i < lotes.Length ? lotes[i++] : Array.Empty<clsSolicitud>());
    }

    [Fact]
    public async Task Refresca_el_score_de_las_activas_que_cambiaron()
    {
        var s = Activa(scoreGuardado: 0m);
        ResponderConLotes(new[] { s });

        var r = await Handler().Handle(new clsRecalcularScoresActivosCommand());

        Assert.Equal(1, r.Revisadas);
        Assert.Equal(1, r.Actualizadas);
        Assert.NotEqual(0m, s.ScorePrioridad);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_escribe_nada_si_ningun_score_cambio()
    {
        // Sin esta guarda, cada pasada tocaría UpdatedAt de todas las activas y la
        // auditoría se llenaría de cambios que no cambiaron nada.
        var alDia = Activa(scoreGuardado: 0m);

        // Se le deja guardado exactamente el score que el recálculo va a calcular, usando
        // la misma regla de Domain que usa el caso de uso.
        alDia.ScorePrioridad = (decimal)clsScorePrioridadCalculator
            .Calcular(alDia, new clsParametrosScore(), Reloj.GetUtcNow()).Total;

        ResponderConLotes(new[] { alDia });

        var r = await Handler().Handle(new clsRecalcularScoresActivosCommand());

        Assert.Equal(1, r.Revisadas);
        Assert.Equal(0, r.Actualizadas);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Recorre_por_lotes_y_guarda_por_lote()
    {
        // Guardar una sola vez al final sería una transacción larga bloqueando filas que
        // la bandeja está leyendo, y un fallo a mitad tiraría todo el trabajo.
        ResponderConLotes(
            new[] { Activa(0m), Activa(0m) },
            new[] { Activa(0m), Activa(0m) },
            new[] { Activa(0m) });

        var r = await Handler().Handle(new clsRecalcularScoresActivosCommand(TamanoDeLote: 2));

        Assert.Equal(5, r.Revisadas);
        Assert.Equal(3, r.Lotes);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Pagina_por_el_ultimo_identificador_y_no_por_desplazamiento()
    {
        // El recálculo escribe sobre las mismas filas que ordena. Con Skip/Take se
        // saltaría solicitudes sin que nadie se entere.
        var primero = new[] { Activa(0m), Activa(0m) };
        var ultimoDelPrimerLote = primero[1].Id;
        ResponderConLotes(primero, new[] { Activa(0m) });

        await Handler().Handle(new clsRecalcularScoresActivosCommand(TamanoDeLote: 2));

        _activas.Verify(a => a.ActivasAsync(null, 2, It.IsAny<CancellationToken>()), Times.Once);
        _activas.Verify(a => a.ActivasAsync(ultimoDelPrimerLote, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Los_parametros_se_leen_una_sola_vez_para_toda_la_corrida()
    {
        // Si alguien recalibra a mitad, mezclar dos juegos de pesos dejaría media
        // bandeja en una escala y media en otra.
        ResponderConLotes(new[] { Activa(0m) }, new[] { Activa(0m) }, new[] { Activa(0m) });

        await Handler().Handle(new clsRecalcularScoresActivosCommand(TamanoDeLote: 1));

        _solicitudes.Verify(s => s.ParametrosScoreAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_activas_no_toca_la_base()
    {
        ResponderConLotes();

        var r = await Handler().Handle(new clsRecalcularScoresActivosCommand());

        Assert.Equal(0, r.Revisadas);
        Assert.Equal(0, r.Lotes);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(100_000, 2_000)]
    public async Task El_tamano_de_lote_se_acota(int pedido, int esperado)
    {
        // Un lote en cero dejaría el bucle pidiendo lotes vacíos para siempre; uno de
        // cien mil vuelve a ser la transacción larga que se está evitando.
        ResponderConLotes();

        await Handler().Handle(new clsRecalcularScoresActivosCommand(pedido));

        _activas.Verify(a => a.ActivasAsync(null, esperado, It.IsAny<CancellationToken>()), Times.Once);
    }
}
