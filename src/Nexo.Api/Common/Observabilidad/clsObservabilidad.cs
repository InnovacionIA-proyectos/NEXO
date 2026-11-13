using System.Diagnostics;
using System.Security.Claims;
using Nexo.Application.Common.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nexo.Api.Common.Observabilidad;

/// <summary>
/// Trazas, métricas y logs de la API hacia el colector central (Alloy → Prometheus / Loki / Jaeger).
/// Se enciende solo con <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>; sin esa variable no se exporta nada y la API se comporta igual que antes.
/// El ambiente viaja en la variable estándar <c>OTEL_RESOURCE_ATTRIBUTES=deployment.environment.name=qa</c>.
/// </summary>
public static class clsObservabilidad
{
    /// <summary>Fuente de los tramos de la capa de aplicación: un tramo por caso de uso (Command o Query).</summary>
    public static readonly ActivitySource CapaDeAplicacion = new("Nexo.Application");

    public static void AddObservabilidad(this WebApplicationBuilder builder)
    {
        builder.Services.TrazarCasosDeUso();

        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"])) return;

        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeScopes = true;
            o.IncludeFormattedMessage = true;
        });

        builder.Services.AddOpenTelemetry()
            // job en Prometheus = "nexo/nexo-api" (namespace/servicio), la convención del servidor de monitoreo.
            .ConfigureResource(r => r.AddService(
                "nexo-api", serviceNamespace: "nexo",
                serviceVersion: typeof(clsObservabilidad).Assembly.GetName().Version?.ToString()))
            .WithTracing(t => t
                .AddSource(CapaDeAplicacion.Name, "MassTransit")
                .SetSampler(new ParentBasedSampler(new clsSinSondeos()))
                // La sonda de salud llega cada 30 s: trazarla tapa las peticiones reales.
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                // El tramo se llama "GET" a secas: con el destino se ve a quién se llamó sin abrirlo. Solo el host, no la ruta (lleva ids).
                .AddHttpClientInstrumentation(o => o.EnrichWithHttpRequestMessage =
                    (tramo, peticion) => tramo.DisplayName = $"{peticion.Method} {peticion.RequestUri?.Host}")
                .AddSqlClientInstrumentation())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddSqlClientInstrumentation()
                .AddMeter("MassTransit"))
            .UseOtlpExporter();
    }

    /// <summary>
    /// Envuelve cada <see cref="ICommandHandler{TCommand,TResult}"/> / <see cref="IQueryHandler{TQuery,TResult}"/> ya registrado con un
    /// decorador que abre un tramo y deja una línea de log. Va por el contenedor para no tocar los handlers uno por uno.
    /// </summary>
    public static IServiceCollection TrazarCasosDeUso(this IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var original = services[i];
            if (!original.ServiceType.IsConstructedGenericType || original.IsKeyedService) continue;

            var definicion = original.ServiceType.GetGenericTypeDefinition();
            var decorador = definicion == typeof(ICommandHandler<,>) ? typeof(clsCommandHandlerTrazado<,>)
                : definicion == typeof(IQueryHandler<,>) ? typeof(clsQueryHandlerTrazado<,>)
                : null;
            if (decorador is null) continue;

            var tipoDecorador = decorador.MakeGenericType(original.ServiceType.GetGenericArguments());
            services[i] = ServiceDescriptor.Describe(
                original.ServiceType,
                sp => ActivatorUtilities.CreateInstance(sp, tipoDecorador,
                    original.ImplementationInstance
                    ?? original.ImplementationFactory?.Invoke(sp)
                    ?? ActivatorUtilities.CreateInstance(sp, original.ImplementationType!)),
                original.Lifetime);
        }
        return services;
    }

    /// <summary>
    /// Una línea por petición (método, ruta, código, duración, usuario, rol y traza) y el usuario como etiqueta de la traza.
    /// De estas líneas sale el tablero "Usuarios y actividad reciente" de Grafana: cambiar los nombres de sus campos lo rompe.
    /// El id de traza va aquí y no como scope en todas las líneas: los scopes de ASP.NET (ConnectionId, RequestId...) triplican el largo
    /// de cada línea de consola. Con este id se abre en Jaeger el recorrido completo; en Loki todas las líneas ya lo llevan.
    /// </summary>
    public static void UseResumenDePeticion(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Nexo.Api.Peticion");
        app.Use(async (contexto, siguiente) =>
        {
            var inicio = Stopwatch.GetTimestamp();
            try
            {
                await siguiente(contexto);
            }
            finally
            {
                var ruta = contexto.Request.Path;
                // Fuera la sonda de salud (ruido cada 30 s) y los hubs (una conexión de SignalR dura horas, no es una petición).
                if (!ruta.StartsWithSegments("/health") && !ruta.StartsWithSegments("/hubs"))
                {
                    var usuario = contexto.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anónimo";
                    var nombre = contexto.User.FindFirstValue(ClaimTypes.Name) ?? "anónimo";
                    var rol = contexto.User.FindFirstValue(ClaimTypes.Role) ?? "sin rol";
                    Activity.Current?.SetTag("enduser.id", usuario).SetTag("enduser.role", rol);

                    var codigo = contexto.Response.StatusCode;
                    logger.Log(
                        codigo >= 500 ? LogLevel.Error : codigo >= 400 ? LogLevel.Warning : LogLevel.Information,
                        "{Metodo} {Ruta} -> {Codigo} en {DuracionMs} ms (usuario {Nombre} [{Usuario}], rol {Rol}, traza {Traza})",
                        contexto.Request.Method, ruta.Value, codigo, (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
                        nombre, usuario, rol, Activity.Current?.TraceId.ToString() ?? "-");
                }
            }
        });
    }

    internal static async Task<TResult> Ejecutar<TResult>(Type handler, string tipo, ILogger logger, Func<Task<TResult>> accion)
    {
        using var tramo = CapaDeAplicacion.StartActivity(handler.Name);
        tramo?.SetTag("nexo.capa", "aplicacion").SetTag("nexo.caso_de_uso.tipo", tipo);
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            var resultado = await accion();
            // Las consultas van en Debug: la bandeja consulta sin parar y en Information taparía lo que sí cambia datos.
            logger.Log(tipo == "command" ? LogLevel.Information : LogLevel.Debug,
                "{CasoDeUso} terminó en {DuracionMs} ms", handler.Name, (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
            return resultado;
        }
        catch (Exception ex)
        {
            tramo?.SetStatus(ActivityStatusCode.Error, ex.Message).AddException(ex);
            // Sin la pila: clsErrorHandlingMiddleware ya la registra completa. Aquí va el dato que a esa línea le falta: qué caso de uso fue.
            logger.LogWarning("{CasoDeUso} falló tras {DuracionMs} ms: {TipoDeError}: {Mensaje}",
                handler.Name, (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds, ex.GetType().Name, ex.Message);
            throw;
        }
    }
}

/// <summary>
/// Descarta las trazas que empiezan en una llamada saliente (SQL o HTTP sin petición ni mensaje que la origine): son los sondeos
/// periódicos del outbox de MassTransit y de los servicios en segundo plano, que en Jaeger tapan las peticiones reales.
/// </summary>
public sealed class clsSinSondeos : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters p)
        => new(p.Kind == ActivityKind.Client ? SamplingDecision.Drop : SamplingDecision.RecordAndSample);
}

internal sealed class clsCommandHandlerTrazado<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> interno, ILoggerFactory loggers)
    : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    public Task<TResult> Handle(TCommand command, CancellationToken ct = default)
        => clsObservabilidad.Ejecutar(interno.GetType(), "command", loggers.CreateLogger(interno.GetType()), () => interno.Handle(command, ct));
}

internal sealed class clsQueryHandlerTrazado<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> interno, ILoggerFactory loggers)
    : IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
{
    public Task<TResult> Handle(TQuery query, CancellationToken ct = default)
        => clsObservabilidad.Ejecutar(interno.GetType(), "query", loggers.CreateLogger(interno.GetType()), () => interno.Handle(query, ct));
}
