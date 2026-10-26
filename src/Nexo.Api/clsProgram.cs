using Nexo.Api.Trabajos;
using Nexo.Application.CasosDeUso.Bandeja.Interfaces;
using Nexo.Application.CasosDeUso.Trabajos.Interfaces;
using Nexo.Application.CasosDeUso.Costeo;
using Nexo.Application.CasosDeUso.IA;
using Nexo.Application.CasosDeUso.DiccionarioCliente;
using Nexo.Application.CasosDeUso.MapeoExcel;
using Nexo.Application.CasosDeUso.Trabajos;
using Nexo.Infrastructure.Common.Mensajeria;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Nexo.Api.Common.Auth;
using Nexo.Api.Common.Errors;
using Nexo.Api.Common.Middleware;
using Nexo.Api.Common.RateLimiting;
using Nexo.Api.Bandeja;
using Nexo.Api.PlanDeCargue;
using Nexo.Api.Priorizacion;
using Nexo.Api.Solicitudes;
using Nexo.Application.CasosDeUso.AjustesVigenciaPlaza;
using Nexo.Application.CasosDeUso.PlanDeCargue;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.CasosDeUso.Biblioteca;
using Nexo.Application.CasosDeUso.BibliotecaSapHistorico;
using Nexo.Application.CasosDeUso.Catalogos;
using Nexo.Application.CasosDeUso.Cotizaciones;
using Nexo.Application.CasosDeUso.Cubicacion;
using Nexo.Application.CasosDeUso.Metricas;
using Nexo.Application.CasosDeUso.Notificaciones;
using Nexo.Application.CasosDeUso.Priorizacion;
using Nexo.Application.CasosDeUso.RangosPeso;
using Nexo.Application.CasosDeUso.ReglasSoloSicetac;
using Nexo.Application.CasosDeUso.ServiciosConexos;
using Nexo.Application.CasosDeUso.Solicitudes;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.CasosDeUso.TarifasGanadas;
using Nexo.Application.CasosDeUso.TarifasPlaza;
using Nexo.Domain.Enums;
using Nexo.Infrastructure;
using Nexo.Infrastructure.Persistence;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
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
// tarifas ganadas) y Métricas (Tablero del Líder). El piso SICE-TAC de Mesa de
// Costeo se resuelve contra DWSice en vivo (issue #179), ya no hay caché local
// con import de Excel. Cubicación 3D y Plan de Cargue son un microservicio
// Python aparte (carpeta cubicador/ de este mismo repo) — NEXO no reimplementa
// el motor, solo le pide trabajos y recibe resultados por cola vía MassTransit
// (ver AddMensajeria() y docs/mensajeria/CUBICACION.md).
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
builder.Services.AddScoped<INotificadorDeTrabajos, clsNotificadorSignalR>();
builder.Services.AddScoped<INotificadorDeBandeja, clsNotificadorDeBandejaSignalR>();
builder.Services.AddScoped<INotificadorDePlanCargue, clsNotificadorPlanCargueSignalR>();
builder.Services.AddPlanDeCargueUseCases();
builder.Services.AddIaUseCases();
builder.Services.AddDiccionarioClienteUseCases();
builder.Services.AddMapeoExcelUseCases();
builder.Services.AddCubicacionUseCases();
builder.Services.AddCotizacionesUseCases();
builder.Services.AddNotificacionesUseCases();
builder.Services.AddReglasSoloSicetacUseCases();
builder.Services.AddAjustesVigenciaPlazaUseCases();
builder.Services.AddBibliotecaUseCases();
builder.Services.AddBibliotecaSapHistoricoUseCases();
builder.Services.AddSolicitudesUseCases();
builder.Services.AddPriorizacionUseCases();
builder.Services.AddServiciosConexosUseCases();
builder.Services.AddTarifasGanadasUseCases();
builder.Services.AddMetricasUseCases();
builder.Services.AddCatalogosUseCases();
builder.Services.AddRangosPesoUseCases();
builder.Services.AddTarifasPlazaUseCases();
// Arma la cadena de priorización al arrancar y se va. NO tiene reloj: el reloj vive en
// la saga, dentro del broker. Reemplaza al RecalculoScoreHostedService, que llevaba un
// PeriodicTimer adentro y por lo tanto se duplicaba con cada réplica del API.
builder.Services.AddHostedService<clsArranqueDeLaCadenaHostedService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, clsCurrentUser>();
builder.Services.AddScoped<Nexo.Application.Common.ICurrentUserPort>(sp => sp.GetRequiredService<ICurrentUser>());

// --------------------------------------------------------------- Autenticación / Autorización
// Fail-closed: Nexo no tiene login propio (docs/contrato-integracion, D1) — la única
// fuente de identidad es la tabla de usuarios/permisos del portal. Sin ella configurada,
// el arranque en Producción falla aquí mismo, antes de exponer un solo endpoint.
var identidadPortalConfigurada = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("IdentidadPortal"));
if (builder.Environment.IsProduction() && !identidadPortalConfigurada)
    throw new InvalidOperationException(
        "ConnectionStrings:IdentidadPortal no está configurado en Producción. Sin la fuente de " +
        "identidad del portal, Nexo no puede resolver sesiones reales. Configure la cadena antes de desplegar.");

// Mismo criterio para el broker: sin cadena configurada la mensajería va en memoria,
// que sirve para pruebas y para levantar el API sin RabbitMQ, pero en Producción
// significaría perder cada mensaje al reiniciar el proceso. Mejor no arrancar.
if (builder.Environment.IsProduction() &&
    string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("RabbitMq")))
    throw new InvalidOperationException(
        "ConnectionStrings:RabbitMq no está configurado en Producción. Sin broker la mensajería " +
        "va en memoria y los mensajes no sobreviven a un reinicio. Configure el broker antes de desplegar.");

builder.Services.AddAuthentication(clsIdentidadPortalAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, clsIdentidadPortalAuthenticationHandler>(
        clsIdentidadPortalAuthenticationHandler.SchemeName, _ => { })
    // Esquema aparte de servicio-a-servicio para el puente hacia el Cubicador (Python) —
    // ver clsLineasSolicitudCubicacionController. No es el default: solo se usa donde el
    // endpoint lo pide explícitamente con [Authorize(AuthenticationSchemes = ...)].
    .AddScheme<AuthenticationSchemeOptions, clsCubicadorApiKeyAuthenticationHandler>(
        clsCubicadorApiKeyAuthenticationHandler.SchemeName, _ => { });

// Autenticación (identidad válida, ver el Handler) y autorización (uno de los 6 roles de
// Nexo, resuelto contra la tabla del portal) son cosas DISTINTAS a propósito: sin esta
// segunda condición en el fallback, alguien con identidad válida pero sin permiso sobre
// Nexo pasaría cualquier endpoint que no declare [Authorize(Roles=...)] — el
// contrato exige rechazarlo en TODOS, no solo en los que restringen por rol específico.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(System.Security.Claims.ClaimTypes.Role, Enum.GetNames<RolUsuario>())
        .Build();
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Rate limiting básico (por rol/IP) en los endpoints de listado más pesados —
// ver clsRateLimitingExtensions para el detalle de las dos políticas.
builder.Services.AddRateLimitingNexo();

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
            var error = new clsApiError("VALIDATION_ERROR", "Una o más validaciones fallaron.", DateTimeOffset.UtcNow, errores);
            return new BadRequestObjectResult(error);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // Sin esto, los /// <summary> de los controllers (varios ya los tenían) nunca
    // llegaban al contrato que consume el frontend en /swagger - quedaban solo
    // como comentario para quien lee el código. GenerateDocumentationFile en
    // Nexo.Api.csproj es el otro lado de este cambio.
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

// El backend lo consumen dos clientes con orígenes distintos: el frontend standalone
// (desarrollo) y el módulo cabina-fletes dentro del portal de la Administración —
// el portal tiene su propio dominio por ambiente (QA, Producción) y Nexo mantiene
// su propia URL base (docs/contrato-integracion/README.md, sección 7), así que la
// llamada del portal es cross-origin. Por eso la configuración admite una lista,
// no un único origen.
const string ClientesCors = "clientes";
// Mismo criterio fail-closed que IdentidadPortal/RabbitMq más arriba: sin esta
// variable en Producción, el fallback de desarrollo (localhost:5173) dejaría el CORS
// mal configurado en silencio en vez de fallar visiblemente al arrancar.
if (builder.Environment.IsProduction() && builder.Configuration["Cors:OrigenesPermitidos"] is null)
    throw new InvalidOperationException(
        "Cors:OrigenesPermitidos no está configurado en Producción. Configure los orígenes permitidos antes de desplegar.");

var origenesPermitidos = (builder.Configuration["Cors:OrigenesPermitidos"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
// "*" es un atajo SOLO para desarrollo/pruebas: WithOrigins("*") no es un comodín (lo
// compara como string literal y termina bloqueando todo), así que hay que pasar por
// AllowAnyOrigin() explícitamente. No usar en QA/Producción — ver README sección 7.
builder.Services.AddCors(o => o.AddPolicy(ClientesCors, p =>
{
    if (origenesPermitidos is [ "*" ])
        p.AllowAnyOrigin();
    else
        p.WithOrigins(origenesPermitidos);
    p.AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddHealthChecks().AddDbContextCheck<clsNexoDbContext>("database");

var app = builder.Build();

// --------------------------------------------------------------- Comando de seed
// `dotnet run --project src/Nexo.Api seed`
if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
    await db.Database.MigrateAsync();
    var seeded = await clsDataSeeder.SeedAsync(db);
    Console.WriteLine(seeded ? "Seed completado." : "Seed omitido: la base ya tenía datos.");
    return;
}

// --------------------------------------------------------------- Migración + seed al arrancar (Docker)
// Honra Database:MigrateOnStartup / Database:SeedOnStartup del entorno (docker-compose).
// MigrateAsync crea la base si no existe y aplica las migraciones pendientes.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Database:SeedOnStartup"))
        await clsDataSeeder.SeedAsync(db);
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

app.UseMiddleware<clsErrorHandlingMiddleware>();
app.UseCors(ClientesCors);
app.UseAuthentication();
app.UseAuthorization();
// Después de Authorization: el particionado por rol (clsRateLimitingExtensions)
// necesita que User.FindFirst(ClaimTypes.Role) ya esté resuelto. Antes de
// MapControllers para que el 429 se decida sin llegar a ningún handler.
app.UseRateLimiter();

app.MapControllers();

// El canal de avisos del avance de trabajos. La ruta es parte del contrato con el
// frontend: es a donde se conecta el cliente para suscribirse a un trabajo.
app.MapHub<clsTrabajosHub>("/hubs/trabajos");

// El canal de la bandeja va aparte del de trabajos a propósito: /hubs/trabajos ya está
// publicado en el contrato como el canal del avance de UN trabajo, con un grupo por
// trabajo. Meterle la bandeja dejaría el nombre mintiendo.
app.MapHub<clsBandejaHub>("/hubs/bandeja");

// Plan de cargue: aparte de /hubs/trabajos por la misma razón que la bandeja — un
// plan de cargue no tiene líneas pendientes ni reintentos técnicos, forzar esa
// forma dejaría el nombre mintiendo.
app.MapHub<clsPlanCargueHub>("/hubs/plan-cargue");
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

/// <summary>Marcador público para que WebApplicationFactory&lt;clsProgram&gt; (tests de integración)
/// pueda referenciar el entry point pese a los top-level statements.</summary>
public partial class clsProgram { }
