// Prueba de carga QA para la HU-05.2 (issue #20, PR #67) — NO forma parte del PR
// original. Para reproducirla: copiar este archivo dentro de
// tests/Nexo.Api.Tests/Mensajeria/ (mismas dependencias del proyecto) y correr
// `dotnet test --filter FullyQualifiedName~BandejaCarga`. No necesita SQL Server ni
// RabbitMQ real: usa el transporte en memoria del arnés de pruebas de MassTransit,
// igual que clsBandejaPushTests.cs. Ver README.md de esta carpeta para el resultado ya
// obtenido.
using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Costeo;
using Nexo.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// clsBandejaPushTests.cs (del propio PR) prueba el agrupamiento con UN pliego a la vez.
/// Esta prueba lo somete a lo que de verdad pasa en producción: varios comerciales
/// cerrando pliegos de tamaños distintos, al mismo tiempo, entrelazados — igual que
/// clsAutoCosteoSagaCargaTests.cs (HU-04.3/04.4) probó que la saga no contamina el conteo
/// entre trabajos, esta prueba confirma que el agrupamiento de la bandeja (1 aviso por
/// pliego cerrado, sin importar cuántas líneas tenga) se sostiene bajo la misma carga
/// concurrente, y que ningún aviso de bandeja se pierde ni se duplica entre trabajos.
/// </summary>
[Trait("Categoria", "Carga")]
public class clsBandejaCargaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;
    private ISagaStateMachineTestHarness<clsAutoCosteoSaga, clsEstadoAutoCosteo> _saga = null!;

    public async Task InitializeAsync()
    {
        _servicios = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(30),
                    testInactivityTimeout: TimeSpan.FromSeconds(3));

                x.AddSagaStateMachine<clsAutoCosteoSaga, clsEstadoAutoCosteo>().InMemoryRepository();
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
        _saga = _bus.GetSagaStateMachineHarness<clsAutoCosteoSaga, clsEstadoAutoCosteo>();
    }

    public async Task DisposeAsync()
    {
        await _bus.Stop();
        await _servicios.DisposeAsync();
    }

    [Fact]
    public async Task Pliegos_concurrentes_de_tamanos_distintos_producen_exactamente_un_aviso_cada_uno()
    {
        // Tamaños desiguales a propósito: si el agrupamiento dependiera de un contador
        // global o de un temporizador compartido en vez del cierre real de CADA saga,
        // mezclar pliegos chicos y grandes en la misma corrida lo delataría.
        var tamanos = new[] { 1, 3, 5, 10, 20, 2, 8, 15, 1, 30, 4, 6, 12, 25, 2, 9, 18, 3, 7, 11 };
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        var trabajos = await Task.WhenAll(tamanos.Select(async lineas =>
        {
            var trabajoId = Guid.NewGuid();
            await _bus.Bus.Publish(new clsAutoCosteoSolicitado(
                trabajoId, Guid.NewGuid(), lineas, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await _bus.Bus.Publish(new clsPliegoRepartido(trabajoId, Guid.NewGuid(), lineas));
            return (trabajoId, lineas);
        }));

        // Todos los desenlaces de todos los pliegos, disparados a la vez y entrelazados —
        // el escenario real: la base recibiendo clsLineaCosteada de decenas de trabajos sin
        // ningún orden entre ellos.
        var desenlaces = trabajos.SelectMany(t => Enumerable.Range(0, t.lineas)
            .Select(_ => (Func<Task>)(() => _bus.Bus.Publish(
                new clsLineaCosteada(t.trabajoId, Guid.NewGuid(), 1_000_000m, "Sicetac")))));
        await Task.WhenAll(desenlaces.Select(f => f()));

        foreach (var (trabajoId, lineas) in trabajos)
            await Esperar(trabajoId, e => e.ConDesenlace == lineas);

        cronometro.Stop();

        var avisosPorTrabajo = trabajos.Select(t => _bus.Published.Select<clsBandejaCambio>()
            .Count(m => m.Context.Message.Motivo == clsMotivoDeCambio.TrabajoTerminado)).ToList();

        // La aserción que importa: exactamente UN aviso de bandeja POR trabajo cerrado,
        // ni uno menos (perdido) ni uno más (duplicado por la concurrencia).
        var totalAvisosBandeja = _bus.Published.Select<clsBandejaCambio>()
            .Count(m => m.Context.Message.Motivo == clsMotivoDeCambio.TrabajoTerminado);
        Assert.Equal(trabajos.Length, totalAvisosBandeja);

        var totalLineas = tamanos.Sum();
        var totalAvisosAvance = trabajos.Sum(t => _bus.Published.Select<clsTrabajoActualizado>()
            .Count(m => m.Context.Message.TrabajoId == t.trabajoId));
        Assert.True(totalAvisosAvance >= totalLineas,
            "El avance sí es por línea; si bajó, algo se perdió en la mensajería, no en el agrupamiento.");

        Assert.True(cronometro.ElapsedMilliseconds < 30_000,
            $"{trabajos.Length} pliegos concurrentes ({totalLineas} líneas) tardaron " +
            $"{cronometro.ElapsedMilliseconds} ms - sospechoso.");
    }

    private async Task<clsEstadoAutoCosteo> Esperar(Guid trabajoId, Func<clsEstadoAutoCosteo, bool> condicion)
    {
        for (var i = 0; i < 200; i++)
        {
            var estado = _saga.Sagas.Contains(trabajoId);
            if (estado is not null && condicion(estado)) return estado;
            await Task.Delay(50);
        }
        throw new TimeoutException($"La saga {trabajoId} no llegó a la condición esperada a tiempo.");
    }
}
