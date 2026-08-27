using System.Text.Json;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Publica `CubicacionSolicitada` directo al exchange `cubicador.eventos` (topic) que
/// declara Cubicador — ver <see cref="MensajesDeCubicacion"/> para el porqué de no
/// usar IPublicadorDeMensajes/MassTransit aquí.
///
/// Conexión propia con RabbitMQ.Client (no IBus de MassTransit): comparte broker con
/// NEXO (misma <c>ConnectionStrings:RabbitMq</c>) pero habla el protocolo AMQP crudo,
/// exactamente como lo hace `worker/main.py` del otro lado — mismo broker físico, dos
/// topologías independientes que no se cruzan (ver
/// cubicador/docs/DECISIONES_TECNICAS.md #9).
/// </summary>
public sealed class CubicacionRabbitAdapter(IConfiguration config) : ICubicacionPort, IAsyncDisposable
{
    private const string Exchange = "cubicador.eventos";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _conexion;
    private IChannel? _canal;

    public async Task SolicitarCubicacionAsync(
        Guid planCargueId, string estrategia, string algoritmo, int? semilla, bool respetarBloqueadas,
        CancellationToken ct)
    {
        var canal = await ObtenerCanalAsync(ct);
        var planIdTexto = planCargueId.ToString();
        var mensaje = new CubicacionSolicitada(
            "optimizar", planIdTexto, planIdTexto,
            new SolicitudDeCubicacion(estrategia, algoritmo, semilla, null, respetarBloqueadas));

        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(mensaje, JsonOpciones);
        var propiedades = new BasicProperties { ContentType = "application/json", Persistent = true };
        await canal.BasicPublishAsync(Exchange, "cubicacion.solicitada", mandatory: false, propiedades, cuerpo, ct);
    }

    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken ct)
    {
        if (_canal is { IsOpen: true }) return _canal;

        await _lock.WaitAsync(ct);
        try
        {
            if (_canal is { IsOpen: true }) return _canal;

            var url = config.GetConnectionString("RabbitMq") ?? config["RabbitMq:Host"]
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:RabbitMq no está configurado - necesario para hablar con Cubicador.");

            var factory = new ConnectionFactory { Uri = new Uri(url) };
            _conexion = await factory.CreateConnectionAsync("cubicacion-publisher", ct);
            _canal = await _conexion.CreateChannelAsync(new CreateChannelOptions(false, false, null, null), ct);
            // Idempotente: Cubicador ya declara este exchange, esto solo confirma que existe
            // antes de publicar (por si NEXO arranca antes que Cubicador la primera vez).
            await _canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
            return _canal;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.CloseAsync();
        if (_conexion is not null) await _conexion.CloseAsync();
    }
}
