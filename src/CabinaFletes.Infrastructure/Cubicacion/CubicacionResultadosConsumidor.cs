using System.Text;
using System.Text.Json;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Consume `plan-de-cargue.generado`/`cubicacion.rechazada` del exchange
/// `cubicador.eventos` y actualiza el `PlanCargue` correspondiente — issue #28,
/// S2-HU-075. Ver <see cref="MensajesDeCubicacion"/> para el porqué de usar
/// RabbitMQ.Client directo en vez de un IConsumer&lt;T&gt; de MassTransit.
///
/// Reintentos + DLQ + alerta: al no ser un IConsumer&lt;T&gt; de MassTransit, no hay
/// UseMessageRetry/Fault&lt;T&gt; automático (eso es exclusivo de la tubería propia de
/// NEXO). Se replica el MISMO patrón que ya usa Cubicador del otro lado del puente
/// (contador `x-intentos` en la cabecera, 3 intentos, cola de error dedicada) en vez
/// de inventar uno nuevo - ver worker/main.py::_mover_a_dlq. Al agotar los intentos,
/// alerta por correo con <see cref="ICorreoPort"/> (mismo mecanismo que
/// AlertaDeFalloConsumer&lt;T&gt; usa para el resto de NEXO, #13).
/// </summary>
public sealed class CubicacionResultadosConsumidor(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<CubicacionResultadosConsumidor> logger)
    : BackgroundService
{
    private const string Exchange = "cubicador.eventos";
    private const string Cola = "nexo.cubicacion.resultados";
    private const string ColaError = "nexo.cubicacion.resultados_error";
    private const int MaxIntentos = 3;
    private static readonly JsonSerializerOptions JsonOpciones = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var url = config.GetConnectionString("RabbitMq") ?? config["RabbitMq:Host"];
        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogWarning("ConnectionStrings:RabbitMq no configurado - el consumidor de resultados de cubicación no arranca.");
            return;
        }

        var factory = new ConnectionFactory { Uri = new Uri(url) };
        await using var conexion = await factory.CreateConnectionAsync("cubicacion-resultados", stoppingToken);
        await using var canal = await conexion.CreateChannelAsync(
            new CreateChannelOptions(false, false, null, null), stoppingToken);

        await canal.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: stoppingToken);
        await canal.QueueDeclareAsync(Cola, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await canal.QueueBindAsync(Cola, Exchange, "plan-de-cargue.generado", cancellationToken: stoppingToken);
        await canal.QueueBindAsync(Cola, Exchange, "cubicacion.rechazada", cancellationToken: stoppingToken);
        await canal.QueueDeclareAsync(ColaError, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += (_, ea) => ProcesarAsync(canal, ea, stoppingToken);
        await canal.BasicConsumeAsync(Cola, autoAck: false, consumidor, stoppingToken);

        logger.LogInformation("Consumidor de resultados de cubicación escuchando en '{Cola}'.", Cola);
        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private async Task ProcesarAsync(IChannel canal, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var intentosPrevios = ObtenerIntentos(ea.BasicProperties.Headers);
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();

            switch (ea.RoutingKey)
            {
                case "plan-de-cargue.generado":
                    var generado = JsonSerializer.Deserialize<PlanDeCargueGenerado>(ea.Body.Span, JsonOpciones)
                        ?? throw new InvalidOperationException("plan-de-cargue.generado sin cuerpo deserializable.");
                    await AplicarResultadoAsync(db, generado.PlanId, generado.Resultado.Estado, generado.Resultado.Puntaje, ct);
                    logger.LogInformation("Plan {PlanId} actualizado a '{Estado}' (puntaje {Puntaje}) desde plan-de-cargue.generado.",
                        generado.PlanId, generado.Resultado.Estado, generado.Resultado.Puntaje);
                    break;

                case "cubicacion.rechazada":
                    var rechazada = JsonSerializer.Deserialize<CubicacionRechazada>(ea.Body.Span, JsonOpciones)
                        ?? throw new InvalidOperationException("cubicacion.rechazada sin cuerpo deserializable.");
                    await AplicarResultadoAsync(db, rechazada.PlanId, "error", 0, ct);
                    logger.LogInformation("Plan {PlanId} marcado 'error' - Cubicador rechazó la solicitud ({Codigo}): {Mensaje}",
                        rechazada.PlanId, rechazada.Codigo, rechazada.Mensaje);
                    break;

                default:
                    logger.LogWarning("Routing key inesperada en {Cola}: {RoutingKey} (ignorada).", Cola, ea.RoutingKey);
                    break;
            }

            await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
        catch (Exception exc)
        {
            logger.LogError(exc, "Fallo procesando mensaje de {RoutingKey} (intento {Intento}/{Max})",
                ea.RoutingKey, intentosPrevios + 1, MaxIntentos);

            if (intentosPrevios + 1 >= MaxIntentos)
            {
                await MoverAErrorAsync(canal, ea, exc.Message, intentosPrevios + 1, ct);
                await AlertarAsync(ea.RoutingKey, exc.Message, intentosPrevios + 1, ct);
            }
            else
            {
                await ReencolarAsync(canal, ea, intentosPrevios + 1, ct);
            }

            await canal.BasicAckAsync(ea.DeliveryTag, multiple: false, ct);
        }
    }

    private static async Task AplicarResultadoAsync(
        CabinaFletesDbContext db, string planIdTexto, string estado, decimal puntaje, CancellationToken ct)
    {
        if (!Guid.TryParse(planIdTexto, out var planId)) return;
        var plan = await db.PlanesCargue.FirstOrDefaultAsync(p => p.Id == planId, ct);
        if (plan is null) return; // el trabajo puede haberse creado y purgado en otro proceso - no hay nada que actualizar

        plan.Estado = estado;
        plan.Puntaje = puntaje;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static int ObtenerIntentos(IDictionary<string, object?>? headers)
    {
        if (headers is not null && headers.TryGetValue("x-intentos", out var valor) && valor is not null)
        {
            return valor switch
            {
                int i => i,
                byte[] bytes => int.Parse(Encoding.UTF8.GetString(bytes)),
                _ => 0,
            };
        }
        return 0;
    }

    private static async Task ReencolarAsync(IChannel canal, BasicDeliverEventArgs ea, int intentos, CancellationToken ct)
    {
        var propiedades = new BasicProperties
        {
            ContentType = "application/json", Persistent = true,
            Headers = new Dictionary<string, object?> { ["x-intentos"] = intentos },
        };
        await canal.BasicPublishAsync(Exchange, ea.RoutingKey, mandatory: false, propiedades, ea.Body, ct);
    }

    private async Task MoverAErrorAsync(IChannel canal, BasicDeliverEventArgs ea, string error, int intentos, CancellationToken ct)
    {
        var propiedades = new BasicProperties
        {
            ContentType = "application/json", Persistent = true,
            Headers = new Dictionary<string, object?> { ["x-intentos"] = intentos, ["x-error"] = error[..Math.Min(error.Length, 1000)] },
        };
        await canal.BasicPublishAsync(string.Empty, ColaError, mandatory: false, propiedades, ea.Body, ct);
        logger.LogError("Mensaje de {RoutingKey} movido a {ColaError} tras {Intentos} intentos: {Error}", ea.RoutingKey, ColaError, intentos, error);
    }

    private async Task AlertarAsync(string routingKey, string error, int intentos, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var correo = scope.ServiceProvider.GetRequiredService<ICorreoPort>();
        if (!correo.Configurado)
        {
            logger.LogWarning("El SMTP corporativo no está configurado - nadie recibió correo por este fallo.");
            return;
        }

        var destinatarios = config.GetSection("Alertas:EquipoTecnico").GetChildren()
            .Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToArray();
        if (destinatarios.Length == 0)
        {
            logger.LogWarning("Alertas:EquipoTecnico está vacío - nadie recibió correo por este fallo.");
            return;
        }

        var asunto = $"[NEXO] Resultado de cubicación ({routingKey}) cayó a la cola de error";
        var cuerpo = $"""
            <p>Un mensaje de <strong>{routingKey}</strong> (resultado de Cubicador) agotó
            sus reintentos ({intentos}) y se movió a <code>{ColaError}</code>.</p>
            <p><strong>Motivo:</strong> {System.Net.WebUtility.HtmlEncode(error)}</p>
            <p>Revisa la consola de administración de RabbitMQ para ver el mensaje completo.</p>
            """;
        foreach (var destinatario in destinatarios)
            await correo.EnviarAsync(destinatario, asunto, cuerpo, null, null, ct);
    }
}
