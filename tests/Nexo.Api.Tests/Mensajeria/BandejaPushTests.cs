using Nexo.Infrastructure.Common.Mensajeria.Saga;
using Nexo.Mensajes.Bandeja;
using Nexo.Mensajes.Costeo;
using Nexo.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// El aviso de que la bandeja cambió, visto desde la saga del auto-costeo.
///
/// LA PRUEBA QUE IMPORTA es la del agrupamiento. Un pliego de 300 líneas produce 300
/// avisos de avance —la barra de progreso los necesita— pero la bandeja solo necesita
/// saber que reconsulte, y una vez basta. Si el aviso saliera por línea, cada pliego
/// grande dispararía cientos de consultas de bandeja completas contra la base, por parte
/// de todos los conectados a la vez.
///
/// El punto donde se agrupa no es un contador ni un temporizador: es que el aviso sale
/// donde el trabajo CIERRA, que por construcción ocurre una sola vez.
/// </summary>
[Trait("Categoria", "Saga")]
public class BandejaPushTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;
    private ISagaStateMachineTestHarness<AutoCosteoSaga, EstadoAutoCosteo> _saga = null!;

    public async Task InitializeAsync()
    {
        _servicios = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(15),
                    testInactivityTimeout: TimeSpan.FromSeconds(2));

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

    private async Task<Guid> IniciarTrabajo(int lineas)
    {
        var trabajoId = Guid.NewGuid();
        await _bus.Bus.Publish(new AutoCosteoSolicitado(
            trabajoId, Guid.NewGuid(), lineas, Guid.NewGuid(), DateTimeOffset.UtcNow));
        await EsperarEstado(trabajoId, e => e.CurrentState == "Procesando");
        await _bus.Bus.Publish(new PliegoRepartido(trabajoId, Guid.NewGuid(), lineas));
        await EsperarEstado(trabajoId, e => e.TotalLineas == lineas);
        return trabajoId;
    }

    private async Task<EstadoAutoCosteo> EsperarEstado(
        Guid trabajoId, Func<EstadoAutoCosteo, bool> condicion, int intentos = 60)
    {
        for (var i = 0; i < intentos; i++)
        {
            var actual = _saga.Sagas.Contains(trabajoId);
            if (actual is not null && condicion(actual)) return actual;
            await Task.Delay(50);
        }
        throw new TimeoutException($"La saga {trabajoId} no alcanzó el estado esperado.");
    }

    private int AvisosDeBandeja(string? motivo = null) =>
        _bus.Published.Select<BandejaCambio>()
            .Count(m => motivo is null || m.Context.Message.Motivo == motivo);

    private int AvisosDeAvance(Guid trabajoId) =>
        _bus.Published.Select<TrabajoActualizado>()
            .Count(m => m.Context.Message.TrabajoId == trabajoId);

    [Fact]
    public async Task Un_pliego_de_muchas_lineas_produce_UN_aviso_de_bandeja()
    {
        // ESTA ES LA PRUEBA QUE JUSTIFICA DÓNDE SE PUBLICA. Con 20 líneas hay 20 avisos de
        // avance y tiene que haber uno solo de bandeja: la barra necesita el detalle, la
        // bandeja solo necesita saber que reconsulte.
        const int lineas = 20;
        var trabajoId = await IniciarTrabajo(lineas);

        for (var i = 0; i < lineas; i++)
            await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_000_000m, "Sicetac"));

        await EsperarEstado(trabajoId, e => e.CurrentState == "Completado");

        Assert.Equal(1, AvisosDeBandeja(MotivoDeCambio.TrabajoTerminado));
        Assert.True(AvisosDeAvance(trabajoId) > lineas,
            "El avance sí tiene que avisar por línea; es la bandeja la que se agrupa.");
    }

    [Fact]
    public async Task No_se_avisa_a_la_bandeja_mientras_el_trabajo_va_a_medias()
    {
        // Con líneas a medio costear la fila sigue en el filtro de pendientes: no ha
        // cambiado nada que la bandeja deba mostrar distinto.
        var trabajoId = await IniciarTrabajo(5);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_000_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 900_000m, "Sicetac"));
        await EsperarEstado(trabajoId, e => e.ConDesenlace == 2);

        Assert.Equal(0, AvisosDeBandeja());
    }

    [Fact]
    public async Task Un_trabajo_que_cierra_parcial_tambien_avisa()
    {
        // Parcial es un desenlace previsto, no un fallo: el trabajo cerró y sus líneas ya
        // no están sin flete de la misma forma. La bandeja tiene que enterarse igual.
        var trabajoId = await IniciarTrabajo(2);

        await _bus.Bus.Publish(new LineaCosteada(trabajoId, Guid.NewGuid(), 1_000_000m, "Sicetac"));
        await _bus.Bus.Publish(new LineaRequiereTarifaManual(
            trabajoId, Guid.NewGuid(), Guid.NewGuid(), "No hay tarifa de plaza vigente."));

        await EsperarEstado(trabajoId, e => e.CurrentState == "Parcial");

        Assert.Equal(1, AvisosDeBandeja(MotivoDeCambio.TrabajoTerminado));
    }

    [Fact]
    public void El_motivo_del_aviso_tiene_que_ser_uno_conocido()
    {
        // Un motivo libre volvería el contrato una convención de pasillo: el cliente
        // compararía contra cadenas escritas de memoria y un error de tipeo llegaría al
        // navegador sin que nada lo frenara.
        var validador = new BandejaCambioValidador();

        Assert.Empty(validador.Validar(
            new BandejaCambio(MotivoDeCambio.TrabajoTerminado, DateTimeOffset.UtcNow)));
        Assert.NotEmpty(validador.Validar(
            new BandejaCambio("se-movio-algo", DateTimeOffset.UtcNow)));
        Assert.NotEmpty(validador.Validar(
            new BandejaCambio(MotivoDeCambio.TrabajoTerminado, default)));
    }
}
