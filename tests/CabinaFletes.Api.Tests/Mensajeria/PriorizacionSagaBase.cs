using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Mensajes.Priorizacion;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// Montaje compartido de las pruebas de la cadena de priorización.
///
/// EL INTERVALO ES PARÁMETRO POR UN MOTIVO CONCRETO, no por elegancia. El arnés de
/// MassTransit espera a que el bus se quede quieto para detenerse, y un tic recurrente
/// nunca lo deja: con un intervalo de 300 ms, cada prueba tardaba 29 segundos en el
/// Stop aunque su afirmación se resolviera en medio segundo.
///
/// Así que las pruebas que no necesitan que el reloj suene usan un intervalo largo —el
/// tic no llega, el bus se queda quieto, la prueba termina en milisegundos— y solo la que
/// verifica el reloj en sí paga el precio de esperarlo.
/// </summary>
public abstract class PriorizacionSagaBase(TimeSpan intervalo) : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    protected ITestHarness Bus = null!;
    protected ISagaStateMachineTestHarness<SagaDePriorizacion, EstadoPriorizacion> Saga = null!;

    protected static Guid Cadena => CadenaDePriorizacion.Unica;

    public async Task InitializeAsync()
    {
        _servicios = new ServiceCollection()
            .AddSingleton(new IntervaloDeRecalculo(intervalo))
            .AddMassTransitTestHarness(x =>
            {
                // Acota lo que el arnés espera al detenerse. Con un tic recurrente el bus
                // nunca se queda quieto, así que sin este tope Stop agotaba el plazo por
                // defecto —52 segundos medidos— en la clase de intervalo corto.
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(8),
                    testInactivityTimeout: TimeSpan.FromSeconds(1));

                x.AddSagaStateMachine<SagaDePriorizacion, EstadoPriorizacion>().InMemoryRepository();

                // El planificador tiene que estar registrado Y usado en la tubería: sin
                // la segunda línea la actividad Schedule falla con
                // "MessageSchedulerContext payload not found".
                x.AddDelayedMessageScheduler();
                x.UsingInMemory((contexto, cfg) =>
                {
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(contexto);
                });
            })
            .BuildServiceProvider(true);

        Bus = _servicios.GetRequiredService<ITestHarness>();
        await Bus.Start();
        Saga = Bus.GetSagaStateMachineHarness<SagaDePriorizacion, EstadoPriorizacion>();
    }

    public async Task DisposeAsync()
    {
        await Bus.Stop();
        await _servicios.DisposeAsync();
    }

    protected async Task<EstadoPriorizacion> Arrancar()
    {
        await Bus.Bus.Publish(new ArrancarPriorizacion(Cadena));
        return await EsperarSaga(s => s.CurrentState == "Programado");
    }

    protected async Task<EstadoPriorizacion> EsperarSaga(
        Func<EstadoPriorizacion, bool> condicion, int intentos = 60)
    {
        for (var i = 0; i < intentos; i++)
        {
            var actual = Saga.Sagas.Contains(Cadena);
            if (actual is not null && condicion(actual)) return actual;
            await Task.Delay(50);
        }
        throw new TimeoutException("La cadena de priorización no alcanzó el estado esperado.");
    }

    protected int PedidosDeRecalculo(string? motivo = null) =>
        Bus.Published.Select<RecalcularScoresSolicitado>()
            .Count(m => motivo is null || m.Context.Message.Motivo == motivo);
}
