using CabinaFletes.Application.CasosDeUso.Notificaciones;
using CabinaFletes.Application.CasosDeUso.Notificaciones.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Infrastructure.Notificaciones.Adaptadores;
using CabinaFletes.Mensajes.Notificaciones;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// Reintentos y cola de error de la notificación, que es el tercer criterio de la HU-06.1.
///
/// LO QUE SE AFIRMA ES LA DIFERENCIA entre las dos clases de fallo, porque tratarlas igual
/// tiene un costo concreto: un adjunto que pesa más de lo permitido va a fallar las cuatro
/// veces, y con los intervalos de producción eso son noventa segundos antes de que la
/// alerta llegue. Marcado como irreparable, el equipo se entera al primer intento.
///
/// Los intervalos aquí van en milisegundos: lo que está bajo prueba es el comportamiento
/// —cuántas veces se intenta y si el fallo queda publicado—, no los tiempos reales.
/// </summary>
[Trait("Categoria", "Saga")]
public class ReintentosDeNotificacionTests
{
    /// <summary>Caso de uso de mentira: falla siempre con la excepción que se le pida.</summary>
    private sealed class NotificacionQueFalla(Func<Exception> fallo)
        : ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto>
    {
        public int Intentos { get; private set; }

        public Task<ResultadoDeNotificacionDto> Handle(
            NotificarEntregaCommand command, CancellationToken ct = default)
        {
            Intentos++;
            throw fallo();
        }
    }

    private static async Task<(int intentos, bool huboFallo)> Correr(Exception excepcion)
    {
        var handler = new NotificacionQueFalla(() => excepcion);

        await using var servicios = new ServiceCollection()
            .AddSingleton<ICommandHandler<NotificarEntregaCommand, ResultadoDeNotificacionDto>>(handler)
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(15),
                    testInactivityTimeout: TimeSpan.FromSeconds(2));

                x.AddConsumer<NotificarEntregaConsumer>();
                x.UsingInMemory((contexto, cfg) =>
                {
                    // Misma forma que en producción: la clase irreparable no se reintenta.
                    cfg.UseMessageRetry(r =>
                    {
                        r.Ignore<CorreoRechazadoException>();
                        r.Intervals(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(20));
                    });
                    cfg.ConfigureEndpoints(contexto);
                });
            })
            .BuildServiceProvider(true);

        var bus = servicios.GetRequiredService<ITestHarness>();
        await bus.Start();
        try
        {
            await bus.Bus.Publish(new CotizacionEntregada(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null, null));

            var huboFallo = await bus.Published.Any<Fault<CotizacionEntregada>>();
            return (handler.Intentos, huboFallo);
        }
        finally
        {
            await bus.Stop();
        }
    }

    [Fact]
    public async Task Un_fallo_pasajero_se_reintenta_y_luego_queda_como_fallo()
    {
        // El relay caído, la red, un 500: reintentar es exactamente lo que corresponde.
        var (intentos, huboFallo) = await Correr(
            new CorreoTransitorioException("El MailSender no respondió."));

        Assert.Equal(3, intentos);   // el original más los dos reintentos configurados
        Assert.True(huboFallo,
            "El fallo tiene que quedar publicado: es de ahí que sale la alerta al equipo.");
    }

    [Fact]
    public async Task Un_rechazo_definitivo_NO_se_reintenta_y_va_derecho_a_la_cola_de_error()
    {
        // Un adjunto demasiado grande o un correo mal formado fallan igual las cuatro
        // veces. Reintentarlo solo retrasa la alerta.
        var (intentos, huboFallo) = await Correr(
            new CorreoRechazadoException("El MailSender rechazó el correo con 413."));

        Assert.Equal(1, intentos);
        Assert.True(huboFallo, "Sin reintentos, pero con fallo publicado: la alerta llega igual.");
    }

    [Fact]
    public async Task Las_dos_clases_de_fallo_existen_y_son_distintas()
    {
        // Si alguien las unifica en una sola excepción, la política pierde el criterio con
        // el que distingue y todo vuelve a reintentarse cuatro veces.
        Assert.NotEqual(typeof(CorreoRechazadoException), typeof(CorreoTransitorioException));
        Assert.IsAssignableFrom<Exception>(new CorreoRechazadoException("x"));
        Assert.IsAssignableFrom<Exception>(new CorreoTransitorioException("x"));
    }
}
