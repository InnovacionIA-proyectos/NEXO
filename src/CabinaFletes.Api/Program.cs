using CabinaFletes.Api.Trabajos;
using CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;
using CabinaFletes.Application.CasosDeUso.Costeo;
using CabinaFletes.Application.CasosDeUso.Trabajos;
using CabinaFletes.Infrastructure.Common.Mensajeria;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using CabinaFletes.Api.Common.Auth;
using CabinaFletes.Api.Common.Errors;
using CabinaFletes.Api.Common.Middleware;
using CabinaFletes.Api.Solicitudes;
using CabinaFletes.Application.CasosDeUso.AjustesVigenciaPlaza;
using CabinaFletes.Application.CasosDeUso.Biblioteca;
using CabinaFletes.Application.CasosDeUso.BibliotecaSapHistorico;
using CabinaFletes.Application.CasosDeUso.Cotizaciones;
using CabinaFletes.Application.CasosDeUso.Metricas;
using CabinaFletes.Application.CasosDeUso.Priorizacion;
using CabinaFletes.Application.CasosDeUso.ReglasSoloSicetac;
using CabinaFletes.Application.CasosDeUso.ServiciosConexos;
using CabinaFletes.Application.CasosDeUso.Solicitudes;
using CabinaFletes.Application.CasosDeUso.TarifasGanadas;
using CabinaFletes.Application.CasosDeUso.TarifasSicetacAdmin;
using CabinaFletes.Infrastructure;
using CabinaFletes.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// -----------------------------------------------------------------------------
// Composition root. Backend migrado en su totalidad: Cotizaciones (piloto),
// ReglasSoloSicetac, AjustesVigenciaPlaza, Biblioteca (TarifarioSap +
// PropuestaTarifa + matriz SICE-TAC — cierra el bloqueo de "Aplicar propuestas"),
// BibliotecaSapHistorico (histórico y tarifas de plaza SAP TM en vivo),
// Solicitudes/Captura (Bandeja Priorizada, alta spot/licitación, auto-costeo),
// ServiciosConexos (catálogo administrado), TarifasGanadas (Biblioteca de
// tarifas ganadas), TarifasSicetacAdmin (caché local + import Excel) y Métricas
// (Tablero del Líder). Cubicación 3D y Plan de Cargue quedan fuera de alcance
// por decisión explícita (microservicio Python aparte) — ver PENDIENTES.md.
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// --------------------------------------------------------------- Infraestructura + Application (Cotizaciones)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMensajeria(builder.Configuration);
builder.Services.AddTrabajosUseCases();
builder.Services.AddCosteoUseCases();

// Canal de avisos. El adaptador vive aquí porque SignalR es ASP.NET Core; el
// consumidor de Infrastructure solo conoce el puerto.
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificadorDeTrabajos, NotificadorSignalR>();
builder.Services.AddCotizacionesUseCases();
builder.Services.AddReglasSoloSicetacUseCases();
builder.Services.AddAjustesVigenciaPlazaUseCases();
builder.Services.AddBibliotecaUseCases();
builder.Services.AddBibliotecaSapHistoricoUseCases();
builder.Services.AddSolicitudesUseCases();
builder.Services.AddPriorizacionUseCases();
builder.Services.AddServiciosConexosUseCases();
builder.Services.AddTarifasGanadasUseCases();
builder.Services.AddTarifasSicetacAdminUseCases();
builder.Services.AddMetricasUseCases();
builder.Services.AddHostedService<RecalculoScoreHostedService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<CabinaFletes.Application.Common.ICurrentUserPort>(sp => sp.GetRequiredService<ICurrentUser>());

// --------------------------------------------------------------- Autenticación / Autorización (igual que el original)
var azureAdClientId = builder.Configuration["AzureAd:ClientId"];
var azureAdConfigured = !string.IsNullOrWhiteSpace(azureAdClientId);

if (builder.Environment.IsProduction() && !azureAdConfigured)
    throw new InvalidOperationException(
        "AzureAd no está configurado en Producción. El esquema de autenticación de desarrollo " +
        "queda deshabilitado por seguridad. Configure la sección 'AzureAd' antes de desplegar.");

// Mismo criterio para el broker: sin cadena configurada la mensajería va en memoria,
// que sirve para pruebas y para levantar el API sin RabbitMQ, pero en Producción
// significaría perder cada mensaje al reiniciar el proceso. Mejor no arrancar.
if (builder.Environment.IsProduction() &&
    string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("RabbitMq")))
    throw new InvalidOperationException(
        "ConnectionStrings:RabbitMq no está configurado en Producción. Sin broker la mensajería " +
        "va en memoria y los mensajes no sobreviven a un reinicio. Configure el broker antes de desplegar.");

var defaultScheme = azureAdConfigured ? JwtBearerDefaults.AuthenticationScheme : DevAuthenticationHandler.SchemeName;
var authBuilder = builder.Services.AddAuthentication(defaultScheme);
if (azureAdConfigured)
{
    var instance = (builder.Configuration["AzureAd:Instance"] ?? "https://login.microsoftonline.com/").TrimEnd('/');
    var tenantId = builder.Configuration["AzureAd:TenantId"];
    authBuilder.AddJwtBearer(options =>
    {
        options.Authority = $"{instance}/{tenantId}/v2.0";
        options.Audience = azureAdClientId;
    });
}
else
{
    authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, _ => { });
}
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// --------------------------------------------------------------- Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errores = ctx.ModelState
                .Where(kv => kv.Value!.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            var error = new ApiError("VALIDATION_ERROR", "Una o más validaciones fallaron.", DateTimeOffset.UtcNow, errores);
            return new BadRequestObjectResult(error);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string FrontendCors = "frontend";
var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"] ?? "http://localhost:5173";
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p => p
    .WithOrigins(frontendOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHealthChecks().AddDbContextCheck<CabinaFletesDbContext>("database");

var app = builder.Build();

// --------------------------------------------------------------- Comando de seed
// `dotnet run --project src/CabinaFletes.Api seed`
if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
    await db.Database.MigrateAsync();
    var seeded = await DataSeeder.SeedAsync(db);
    Console.WriteLine(seeded ? "Seed completado." : "Seed omitido: la base ya tenía datos.");
    return;
}

// --------------------------------------------------------------- Migración + seed al arrancar (Docker)
// Honra Database:MigrateOnStartup / Database:SeedOnStartup del entorno (docker-compose).
// MigrateAsync crea la base si no existe y aplica las migraciones pendientes.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
        await DataSeeder.SeedAsync(db);
}

app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// El canal de avisos del avance de trabajos. La ruta es parte del contrato con el
// frontend: es a donde se conecta el cliente para suscribirse a un trabajo.
app.MapHub<TrabajosHub>("/hubs/trabajos");
// /health responde JSON con el detalle por chequeo, no solo "Healthy"/"Unhealthy".
//
// El motivo es concreto: un 503 con el cuerpo en texto plano no dice QUÉ está caído.
// Diagnosticar uno costó varias corridas hasta resolver HealthCheckService a mano para
// ver que el chequeo en rojo era el del bus. Con esto se lee en la respuesta.
//
// Es una sonda de readiness: incluye el bus de mensajería, así que hay una ventana al
// arrancar en la que responde 503 con "Not ready: not started". Es correcto — hasta que
// el bus no está listo el proceso no debería recibir tráfico — y el compose ya lo
// contempla con start_period.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (contexto, reporte) =>
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";
        await contexto.Response.WriteAsJsonAsync(new
        {
            status = reporte.Status.ToString(),
            duracion = reporte.TotalDuration.TotalMilliseconds,
            chequeos = reporte.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    descripcion = e.Value.Description,
                    error = e.Value.Exception?.Message
                })
        });
    }
}).AllowAnonymous();

app.Run();

/// <summary>Marcador público para que WebApplicationFactory&lt;Program&gt; (tests de integración)
/// pueda referenciar el entry point pese a los top-level statements.</summary>
public partial class Program { }
