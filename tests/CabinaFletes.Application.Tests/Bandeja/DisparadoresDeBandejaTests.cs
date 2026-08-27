using CabinaFletes.Application.CasosDeUso.Priorizacion;
using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.Enums;
using CabinaFletes.Domain.Priorizacion.Entidades;
using CabinaFletes.Domain.Solicitudes.Entidades;
using CabinaFletes.Mensajes.Bandeja;
using Moq;

namespace CabinaFletes.Application.Tests.Bandeja;

/// <summary>
/// Los casos de uso que cambian la bandeja tienen que avisarlo.
///
/// El alta de solicitud se prueba junto a su caso de uso; aquí van los otros dos que
/// mueven filas: el rechazo, que saca una de la cola, y la recalibración de los
/// parámetros, que reordena todas.
///
/// LA RECALIBRACIÓN ES EL CASO INTERESANTE. Refrescar el puntaje persistido NO cambia lo
/// que la bandeja muestra —la bandeja lo recalcula al leer— pero cambiar el horizonte SÍ,
/// porque cambia la rampa con la que se calcula. Por eso el aviso sale de aquí y no del
/// trabajo de refresco.
/// </summary>
public class DisparadoresDeBandejaTests
{
    private static readonly TimeProvider Reloj = TimeProvider.System;

    // ------------------------------------------------------------------ rechazo
    private readonly Mock<ISolicitudesRepository> _solicitudes = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuarioActual = new();
    private readonly Mock<IPublicadorDeMensajes> _publicador = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private RechazarSolicitudCommandHandler RechazarHandler() => new(
        _solicitudes.Object, _auditoria.Object, _usuarioActual.Object, Reloj,
        _publicador.Object, _uow.Object);

    [Fact]
    public async Task Rechazar_una_solicitud_avisa_que_la_bandeja_cambio()
    {
        // Sale de la cola de trabajo: quien la esté mirando tiene que dejar de verla sin
        // tener que refrescar.
        var id = Guid.NewGuid();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Solicitud { Id = id, Estado = EstadoSolicitud.Pendiente });

        await RechazarHandler().Handle(new RechazarSolicitudCommand(id, "precio", null));

        _publicador.Verify(p => p.PublicarAsync(
            It.Is<BandejaCambio>(m => m.Motivo == MotivoDeCambio.SolicitudRechazada),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rechazar_una_ya_descartada_no_avisa_dos_veces()
    {
        // Es idempotente: la fila ya salió de la bandeja. Avisar otra vez sería mandar a
        // todos los conectados a reconsultar para ver exactamente lo mismo.
        var id = Guid.NewGuid();
        _solicitudes.Setup(s => s.ObtenerBasicoAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Solicitud { Id = id, Estado = EstadoSolicitud.Descartada });

        try
        {
            await RechazarHandler().Handle(new RechazarSolicitudCommand(id, "precio", null));
        }
        catch
        {
            // Da igual si el caso de uso corta con excepción o sale en silencio: lo que se
            // afirma es que no se avisa un cambio que no ocurrió.
        }

        _publicador.Verify(p => p.PublicarAsync(
            It.IsAny<BandejaCambio>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ----------------------------------------------------------- recalibración
    [Fact]
    public async Task Recalibrar_los_parametros_avisa_que_la_bandeja_cambio()
    {
        var parametros = new Mock<IParametrosScorePort>();
        parametros.Setup(p => p.ParaEditarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParametrosScore());
        var publicador = new Mock<IPublicadorDeMensajes>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new ActualizarParametrosScoreCommandHandler(
            parametros.Object, publicador.Object, Reloj, uow.Object);

        await handler.Handle(new ActualizarParametrosScoreCommand(
            new ParametrosScoreDto(0.40, 0.30, 0.20, 0.10, 240, 100_000_000m, 100, 50, 30, 50),
            Guid.NewGuid()));

        publicador.Verify(p => p.PublicarAsync(
            It.Is<BandejaCambio>(m => m.Motivo == MotivoDeCambio.ParametrosRecalibrados),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_recalibracion_rechazada_no_avisa_a_la_bandeja()
    {
        // Los pesos no suman 1.0: no se guardó nada, así que la bandeja no cambió.
        var parametros = new Mock<IParametrosScorePort>();
        parametros.Setup(p => p.ParaEditarAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParametrosScore());
        var publicador = new Mock<IPublicadorDeMensajes>();
        var uow = new Mock<IUnitOfWork>();

        var handler = new ActualizarParametrosScoreCommandHandler(
            parametros.Object, publicador.Object, Reloj, uow.Object);

        await Assert.ThrowsAnyAsync<Exception>(() => handler.Handle(
            new ActualizarParametrosScoreCommand(
                new ParametrosScoreDto(0.90, 0.90, 0.90, 0.90, 240, 100_000_000m, 100, 50, 30, 50),
                Guid.NewGuid())));

        publicador.Verify(p => p.PublicarAsync(
            It.IsAny<BandejaCambio>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
