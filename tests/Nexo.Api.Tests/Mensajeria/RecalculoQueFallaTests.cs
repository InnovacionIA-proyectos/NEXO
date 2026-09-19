using Nexo.Mensajes.Priorizacion;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// "El recálculo falla sin afectar mi trabajo", que es uno de los criterios de cierre.
///
/// Antes esto vivía en un BackgroundService del mismo proceso que el API. Un fallo dejaba
/// una línea en el log y se esperaba quince minutos a la siguiente vuelta; si el fallo era
/// permanente, los scores se congelaban y nadie se enteraba hasta que un reporte no
/// cuadrara.
///
/// Ahora la excepción sube a propósito: el bus reintenta con su política escalonada y, si
/// los agota, el mensaje queda como fallo — que es de donde la #13 saca la alerta al
/// equipo. La diferencia con el consumidor de avisos de la #17 es deliberada: allá un
/// fallo se registra y se sigue, porque un navegador desconectado no es una avería. Aquí
/// sí lo es.
/// </summary>
[Trait("Categoria", "Saga")]
public class RecalculoQueFallaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;

    /// <summary>Falla siempre, para poder observar qué hace el bus cuando se agotan los reintentos.</summary>
    private sealed class ConsumidorQueSiempreFalla : IConsumer<RecalcularScoresSolicitado>
    {
        public static int Intentos;

        public Task Consume(ConsumeContext<RecalcularScoresSolicitado> contexto)
        {
            Interlocked.Increment(ref Intentos);
            throw new InvalidOperationException("La base no responde.");
        }
    }

    public async Task InitializeAsync()
    {
        ConsumidorQueSiempreFalla.Intentos = 0;

        _servicios = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(15),
                    testInactivityTimeout: TimeSpan.FromSeconds(2));

                x.AddConsumer<ConsumidorQueSiempreFalla>();
                x.UsingInMemory((contexto, cfg) =>
                {
                    // Reintentos cortos: lo que se afirma es el COMPORTAMIENTO —reintentar
                    // y luego rendirse marcando el fallo—, no los intervalos de producción.
                    // Con los reales (1s, 5s, 15s, 60s) esta prueba tardaría minuto y medio.
                    cfg.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromMilliseconds(10),
                        TimeSpan.FromMilliseconds(20)));
                    cfg.ConfigureEndpoints(contexto);
                });
            })
            .BuildServiceProvider(true);

        _bus = _servicios.GetRequiredService<ITestHarness>();
        await _bus.Start();
    }

    public async Task DisposeAsync()
    {
        await _bus.Stop();
        await _servicios.DisposeAsync();
    }

    [Fact]
    public async Task Un_recalculo_que_falla_se_reintenta_y_luego_queda_marcado_como_fallo()
    {
        await _bus.Bus.Publish(new RecalcularScoresSolicitado(
            CadenaDePriorizacion.Unica, CadenaDePriorizacion.PorElReloj));

        Assert.True(await _bus.Published.Any<Fault<RecalcularScoresSolicitado>>(),
            "El fallo tiene que quedar publicado: es de ahí que sale la alerta al equipo. " +
            "Si se tragara la excepción, un recálculo permanentemente roto pasaría inadvertido.");

        // Tres pasadas: el intento original más los dos reintentos configurados. Que
        // reintente importa porque el fallo típico es transitorio —la base ocupada, una
        // conexión que se cae— y rendirse al primero llenaría la cola de error de ruido.
        Assert.Equal(3, ConsumidorQueSiempreFalla.Intentos);
    }

    [Fact]
    public async Task El_bus_sigue_atendiendo_despues_del_fallo()
    {
        // Lo que pide el criterio: el fallo del recálculo no puede dejar el resto sin
        // atender. Con un BackgroundService, una excepción no manejada en el bucle podía
        // terminar el servicio entero.
        await _bus.Bus.Publish(new RecalcularScoresSolicitado(
            CadenaDePriorizacion.Unica, CadenaDePriorizacion.PorElReloj));
        Assert.True(await _bus.Published.Any<Fault<RecalcularScoresSolicitado>>());

        var antes = ConsumidorQueSiempreFalla.Intentos;

        await _bus.Bus.Publish(new RecalcularScoresSolicitado(
            CadenaDePriorizacion.Unica, CadenaDePriorizacion.PorCambioDeParametros));

        // El segundo mensaje se atiende igual, aunque el primero haya terminado en fallo.
        for (var i = 0; i < 60 && ConsumidorQueSiempreFalla.Intentos <= antes; i++)
            await Task.Delay(50);

        Assert.True(ConsumidorQueSiempreFalla.Intentos > antes,
            "El segundo mensaje no se atendió: el fallo del primero dejó el endpoint parado.");
    }
}
