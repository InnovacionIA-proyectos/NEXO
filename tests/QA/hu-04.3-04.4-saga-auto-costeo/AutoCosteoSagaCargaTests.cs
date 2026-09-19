// Prueba de carga QA para la HU-04.3/04.4 (issues #15/#16, PR #62) — NO forma parte
// del PR original. Para reproducirla: copiar este archivo dentro de
// tests/CabinaFletes.Api.Tests/Mensajeria/ (mismas dependencias del proyecto) y
// correr `dotnet test --filter FullyQualifiedName~AutoCosteoSagaCarga`. No necesita
// SQL Server ni RabbitMQ real: usa el transporte en memoria del arnés de pruebas de
// MassTransit, igual que AutoCosteoSagaTests.cs. Ver README.md de esta carpeta para
// el resultado ya obtenido.
using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Mensajes.Costeo;
using CabinaFletes.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// Prueba de carga QA (no forma parte del PR original): muchos trabajos de
/// auto-costeo en curso a la vez, cada uno con su propio TrabajoId (el
/// CorrelationId de la saga). Confirma que la máquina de estados no
/// contamina el conteo de un trabajo con los desenlaces de otro cuando
/// llegan entrelazados — la garantía que hace "pura" a la saga.
/// </summary>
[Trait("Categoria", "Carga")]
public class AutoCosteoSagaCargaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;
    private ISagaStateMachineTestHarness<AutoCosteoSaga, EstadoAutoCosteo> _saga = null!;

    public async Task InitializeAsync()
    {
        _servicios = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<AutoCosteoSaga, EstadoAutoCosteo>().InMemoryRepository();
                x.AddDelayedMessageScheduler();
                x.UsingInMemory((contexto, cfg) =>
                {
                    cfg.UseDelayedMessageScheduler();
                    cfg.ConfigureEndpoints(contexto);
                });
            })
            .BuildServiceProvider(true);

        _bus = _servicios.GetRequiredService<ITestHarness>();
        await _bus.Start();
        _saga = _bus.GetSagaStateMachineHarness<AutoCosteoSaga, EstadoAutoCosteo>();
    }

    public async Task DisposeAsync()
    {
        await _bus.Stop();
        await _servicios.DisposeAsync();
    }

    [Fact]
    public async Task Trabajos_concurrentes_no_contaminan_el_conteo_entre_si()
    {
        const int trabajos = 20;
        const int lineasPorTrabajo = 5;
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        // Cada trabajo arranca y reparte, todos entrelazados a propósito - es el
        // escenario real: varios comerciales cargando pliegos al mismo tiempo.
        var ids = await Task.WhenAll(Enumerable.Range(0, trabajos).Select(async _ =>
        {
            var trabajoId = Guid.NewGuid();
            var solicitudId = Guid.NewGuid();
            await _bus.Bus.Publish(new AutoCosteoSolicitado(
                trabajoId, solicitudId, lineasPorTrabajo, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await _bus.Bus.Publish(new PliegoRepartido(trabajoId, solicitudId, lineasPorTrabajo));
            return trabajoId;
        }));

        // Todos los desenlaces de todos los trabajos, disparados a la vez y
        // entrelazados - no en el orden en que se crearon los trabajos.
        var desenlaces = ids.SelectMany(id => Enumerable.Range(0, lineasPorTrabajo)
            .Select(_ => (Func<Task>)(() => _bus.Bus.Publish(
                new LineaCosteada(id, Guid.NewGuid(), 1_000_000m, "Sicetac")))));
        await Task.WhenAll(desenlaces.Select(f => f()));

        foreach (var id in ids)
        {
            var estado = await Esperar(id, e => e.ConDesenlace == lineasPorTrabajo);
            Assert.Equal(lineasPorTrabajo, estado.Resueltas);
            Assert.Equal(0, estado.RequierenManual);
            Assert.Equal(0, estado.PendientesTecnicas);
            Assert.Equal(nameof(AutoCosteoSaga.Completado), estado.CurrentState);
        }

        cronometro.Stop();
        Assert.True(cronometro.ElapsedMilliseconds < 30_000,
            $"{trabajos} trabajos concurrentes tardaron {cronometro.ElapsedMilliseconds} ms - sospechoso.");
    }

    private async Task<EstadoAutoCosteo> Esperar(Guid trabajoId, Func<EstadoAutoCosteo, bool> condicion)
    {
        for (var i = 0; i < 100; i++)
        {
            var estado = _saga.Sagas.Contains(trabajoId);
            if (estado is not null && condicion(estado)) return estado;
            await Task.Delay(50);
        }
        throw new TimeoutException($"La saga {trabajoId} no llegó a la condición esperada a tiempo.");
    }
}
