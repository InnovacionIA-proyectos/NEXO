using CabinaFletes.Infrastructure.Common.Mensajeria.Saga;
using CabinaFletes.Mensajes.Costeo;
using CabinaFletes.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// Comportamiento de la saga del auto-costeo.
///
/// No necesita el consumidor de costeo (issue #16) ni un broker: el arnés de MassTransit
/// permite mandarle los mensajes y observar qué publica y a qué estado va. Es la forma
/// correcta de probar una máquina de estados — se prueban las transiciones, no el
/// trabajo que hacen otros.
///
/// La saga se prueba en memoria a propósito. Su persistencia se verifica aparte; aquí
/// lo que está en juego es la orquestación.
/// </summary>
[Trait("Categoria", "Saga")]
public class AutoCosteoSagaTests : IAsyncLifetime
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

                // Registrar el planificador NO basta: el bus tiene que usarlo en su
                // tubería, o la actividad Schedule de la saga falla con
                // "MessageSchedulerContext payload not found". Es la misma línea que
                // hace falta en la configuración de producción.
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

    private async Task<Guid> IniciarTrabajo(int totalLineas)
    {
        var trabajoId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();

        await _bus.Bus.Publish(new AutoCosteoSolicitado(
            trabajoId, solicitudId, totalLineas, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.True(await _saga.Consumed.Any<AutoCosteoSolicitado>());

        await _bus.Bus.Publish(new PliegoRepartido(trabajoId, solicitudId, totalLineas));
        Assert.True(await _saga.Consumed.Any<PliegoRepartido>());

        return trabajoId;
    }

    private EstadoAutoCosteo Estado(Guid trabajoId) =>
        _saga.Sagas.Contains(trabajoId) ?? throw new InvalidOperationException("La saga no existe.");

    [Fact]
    public async Task Al_iniciar_pide_repartir_el_pliego_y_no_lo_lee_ella_misma()
    {
        var trabajoId = Guid.NewGuid();

        await _bus.Bus.Publish(new AutoCosteoSolicitado(
            trabajoId, Guid.NewGuid(), 3, Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.True(await _saga.Consumed.Any<AutoCosteoSolicitado>());
        Assert.True(await _bus.Published.Any<RepartirLineasDeCosteo>(
            m => m.Context.Message.TrabajoId == trabajoId));
    }

    [Fact]
    public async Task Con_todas_las_lineas_resueltas_el_trabajo_queda_Completado()
    {
        var trabajoId = await IniciarTrabajo(2);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_500_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 900_000m, "Tarifario"));

        var estado = await EsperarEstado(trabajoId, e => e.ConDesenlace == 2);
        Assert.Equal(2, estado.Resueltas);
        Assert.Equal(0, estado.RequierenManual);
        Assert.NotNull(estado.TerminadoEn);
        Assert.Equal(nameof(AutoCosteoSaga.Completado), estado.CurrentState);
    }

    [Fact]
    public async Task Una_linea_esperando_a_una_persona_cierra_el_trabajo_en_Parcial_y_no_en_Fallido()
    {
        var trabajoId = await IniciarTrabajo(3);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_200_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 800_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaRequiereTarifaManual(
            trabajoId, Guid.NewGuid(), Guid.NewGuid(), "No hay tarifa de plaza vigente para la ruta."));

        var estado = await EsperarEstado(trabajoId, e => e.ConDesenlace == 3);
        Assert.Equal(2, estado.Resueltas);
        Assert.Equal(1, estado.RequierenManual);
        Assert.Equal(0, estado.PendientesTecnicas);
        Assert.Equal(nameof(AutoCosteoSaga.Parcial), estado.CurrentState);
    }

    [Fact]
    public async Task Los_dos_tipos_de_pendiente_se_cuentan_por_separado()
    {
        var trabajoId = await IniciarTrabajo(3);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_000_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaFalloTecnico(trabajoId, Guid.NewGuid(), "DwSice no respondió."));
        await _bus.Bus.Publish(new LineaRequiereTarifaManual(trabajoId, Guid.NewGuid(), Guid.NewGuid(), "Tarifa de plaza vencida."));

        var estado = await EsperarEstado(trabajoId, e => e.ConDesenlace == 3);

        // El motivo de tenerlos separados: la pantalla dice cosas distintas y el usuario
        // hace cosas distintas. Una se resuelve sola, la otra necesita que alguien actúe.
        Assert.Equal(1, estado.PendientesTecnicas);
        Assert.Equal(1, estado.RequierenManual);
        Assert.Equal(nameof(AutoCosteoSaga.Parcial), estado.CurrentState);
    }

    [Fact]
    public async Task El_reparto_que_llega_tarde_no_cierra_el_trabajo_antes_de_tiempo()
    {
        var trabajoId = Guid.NewGuid();
        var solicitudId = Guid.NewGuid();

        await _bus.Bus.Publish(new AutoCosteoSolicitado(
            trabajoId, solicitudId, 2, Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.True(await _saga.Consumed.Any<AutoCosteoSolicitado>());

        // Un resultado llega ANTES de saberse el total: sin total, la saga no puede
        // concluir que terminó.
        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 500_000m, "Sicetac"));
        var enCurso = await EsperarEstado(trabajoId, e => e.Resueltas == 1);
        Assert.Null(enCurso.TotalLineas);
        Assert.Equal(nameof(AutoCosteoSaga.Procesando), enCurso.CurrentState);

        await _bus.Bus.Publish(new PliegoRepartido(trabajoId, solicitudId, 1));

        var cerrado = await EsperarEstado(trabajoId, e => e.TotalLineas == 1);
        Assert.Equal(nameof(AutoCosteoSaga.Completado), cerrado.CurrentState);
    }

    [Fact]
    public async Task Un_resultado_que_llega_despues_del_cierre_se_ignora_sin_reventar()
    {
        var trabajoId = await IniciarTrabajo(1);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 700_000m, "Sicetac"));
        var cerrado = await EsperarEstado(trabajoId, e => e.ConDesenlace == 1);
        Assert.Equal(nameof(AutoCosteoSaga.Completado), cerrado.CurrentState);

        // Un reintento que se resolvió después del cierre. Pasa de verdad.
        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 300_000m, "Historica"));
        await Task.Delay(200);

        var despues = Estado(trabajoId);
        Assert.Equal(nameof(AutoCosteoSaga.Completado), despues.CurrentState);
        Assert.Equal(1, despues.Resueltas);
    }

    private async Task<EstadoAutoCosteo> EsperarEstado(
        Guid trabajoId, Func<EstadoAutoCosteo, bool> condicion, int intentos = 50)
    {
        for (var i = 0; i < intentos; i++)
        {
            var actual = _saga.Sagas.Contains(trabajoId);
            if (actual is not null && condicion(actual)) return actual;
            await Task.Delay(50);
        }
        throw new TimeoutException($"La saga {trabajoId} no alcanzó el estado esperado.");
    }
}
