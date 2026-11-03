using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Notificaciones.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Infrastructure.Notificaciones.Adaptadores;
using Nexo.Mensajes.Notificaciones;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// S5-HU-20.1/#286: el consumidor del aviso "Cotización actualizada" es delgado —traduce el
/// evento en la llamada al caso de uso— y, como el de la entrega, deja SUBIR la excepción: un
/// aviso perdido no se recupera consultando nada, así que el bus tiene que verlo y publicar
/// el fallo (de ahí sale la alerta al equipo).
/// </summary>
[Trait("Categoria", "Saga")]
public class clsConexosCambiadosConsumerTests
{
    private sealed class clsNotificadorDeMentira(Func<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto> respuesta)
        : ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto>
    {
        public List<clsNotificarConexosCambiadosCommand> Recibidos { get; } = [];

        public Task<clsResultadoDeNotificacionDto> Handle(clsNotificarConexosCambiadosCommand command, CancellationToken ct = default)
        {
            Recibidos.Add(command);
            return Task.FromResult(respuesta(command));
        }
    }

    private static clsConexosCotizacionCambiados Evento() => new(
        Guid.NewGuid(), Guid.NewGuid(), "agregado", [new clsConexoCambiado("Cargue", 2, 100_000m)],
        1_900_000m, Guid.NewGuid(), DateTimeOffset.UtcNow);

    private static async Task<(clsNotificadorDeMentira handler, bool consumido, bool huboFallo)> Correr(
        Func<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto> respuesta)
    {
        var handler = new clsNotificadorDeMentira(respuesta);
        await using var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ICommandHandler<clsNotificarConexosCambiadosCommand, clsResultadoDeNotificacionDto>>(handler)
            .AddMassTransitTestHarness(x =>
            {
                x.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(15), testInactivityTimeout: TimeSpan.FromSeconds(2));
                x.AddConsumer<clsNotificarConexosCambiadosConsumer>();
                x.UsingInMemory((contexto, cfg) =>
                {
                    cfg.UseMessageRetry(r => r.Intervals(TimeSpan.FromMilliseconds(10)));
                    cfg.ConfigureEndpoints(contexto);
                });
            })
            .BuildServiceProvider(true);

        var harness = servicios.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(Evento());
            var consumido = await harness.Consumed.Any<clsConexosCotizacionCambiados>();
            var huboFallo = await harness.Published.Any<Fault<clsConexosCotizacionCambiados>>();
            return (handler, consumido, huboFallo);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task El_evento_llega_al_caso_de_uso_con_el_mismo_contenido()
    {
        var (handler, consumido, huboFallo) = await Correr(c => clsResultadoDeNotificacionDto.Enviada("a@b.co", "id", 0));

        Assert.True(consumido);
        Assert.False(huboFallo);
        var recibido = Assert.Single(handler.Recibidos);
        Assert.Equal("agregado", recibido.Evento.Accion);
        Assert.Equal(1_900_000m, recibido.Evento.NuevoTotal);
    }

    [Fact]
    public async Task Que_no_haya_a_quien_escribirle_no_es_un_fallo()
    {
        var (_, consumido, huboFallo) = await Correr(c => clsResultadoDeNotificacionDto.NoHabiaQueEnviar("sin creador"));

        Assert.True(consumido);
        Assert.False(huboFallo);
    }

    [Fact]
    public async Task Un_fallo_del_caso_de_uso_sube_y_queda_publicado_como_fallo()
    {
        var (handler, _, huboFallo) = await Correr(c => throw new InvalidOperationException("relay caído"));

        Assert.True(huboFallo, "El fallo tiene que quedar publicado: es de ahí que sale la alerta al equipo.");
        Assert.True(handler.Recibidos.Count >= 2, "se reintentó antes de darlo por perdido");
    }
}
