using CabinaFletes.Application.CasosDeUso.Priorizacion;
using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Infrastructure.Priorizacion.Adaptadores;
using CabinaFletes.Mensajes.Bandeja;
using CabinaFletes.Mensajes.Priorizacion;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// El latido de la bandeja, que es el disparador con la razón menos obvia de los cinco.
///
/// El recálculo periódico refresca el puntaje PERSISTIDO, y eso no cambia lo que la
/// bandeja muestra: la bandeja lo recalcula al leer. Entonces, ¿por qué avisar?
///
/// Porque lo que sí cambió desde el aviso anterior es EL TIEMPO. La urgencia depende del
/// ahora, así que el orden se mueve solo, de forma continua, sin que nadie toque nada. Ese
/// movimiento no tiene ningún evento que lo represente y este latido es lo único que lo
/// cubre — por eso sale aunque se hayan actualizado cero filas.
///
/// Si alguien "optimiza" esto para avisar solo cuando cambió algún puntaje, la bandeja
/// abierta se queda con el orden de hace horas y nadie entiende por qué.
/// </summary>
[Trait("Categoria", "Saga")]
public class LatidoDeBandejaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;

    /// <summary>Recálculo que no cambia nada: revisa filas y no actualiza ninguna.</summary>
    private sealed class RecalculoQueNoCambiaNada
        : ICommandHandler<RecalcularScoresActivosCommand, ResultadoDeRecalculoDto>
    {
        public Task<ResultadoDeRecalculoDto> Handle(
            RecalcularScoresActivosCommand command, CancellationToken ct = default) =>
            Task.FromResult(new ResultadoDeRecalculoDto(Revisadas: 40, Actualizadas: 0, Lotes: 1));
    }

    public async Task InitializeAsync()
    {
        _servicios = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddScoped<ICommandHandler<RecalcularScoresActivosCommand, ResultadoDeRecalculoDto>,
                RecalculoQueNoCambiaNada>()
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(
                    testTimeout: TimeSpan.FromSeconds(15),
                    testInactivityTimeout: TimeSpan.FromSeconds(2));

                x.AddConsumer<RecalcularScoresConsumer>();
                x.UsingInMemory((contexto, cfg) => cfg.ConfigureEndpoints(contexto));
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
    public async Task El_recalculo_avisa_a_la_bandeja_aunque_no_cambie_ningun_puntaje()
    {
        await _bus.Bus.Publish(new RecalcularScoresSolicitado(
            CadenaDePriorizacion.Unica, CadenaDePriorizacion.PorElReloj));

        Assert.True(await _bus.Published.Any<BandejaCambio>(),
            "Sin este aviso, la bandeja abierta se queda con el orden de hace horas: la " +
            "urgencia depende del ahora y ese cambio no tiene ningún otro evento.");

        var aviso = _bus.Published.Select<BandejaCambio>().First().Context.Message;
        Assert.Equal(MotivoDeCambio.RefrescoPeriodico, aviso.Motivo);
    }

    [Fact]
    public async Task Cada_recalculo_produce_un_solo_latido()
    {
        // Uno por recálculo, no uno por lote ni por solicitud revisada.
        await _bus.Bus.Publish(new RecalcularScoresSolicitado(
            CadenaDePriorizacion.Unica, CadenaDePriorizacion.PorElReloj));
        Assert.True(await _bus.Published.Any<BandejaCambio>());

        // Se espera un poco por si llegaran más.
        await Task.Delay(500);

        Assert.Single(_bus.Published.Select<BandejaCambio>());
    }
}
