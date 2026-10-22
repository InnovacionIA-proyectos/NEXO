using Nexo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Infra;

public class clsApiFactory : WebApplicationFactory<clsProgram>
{
    // Cadena de la BD de pruebas. Parametrizable por la variable de entorno TEST_DB_CONNECTION
    // (igual criterio que el proyecto original): así se puede usar auth SQL contra un SQL
    // Server en Docker/Linux —que no acepta Trusted_Connection (auth Windows)— sin hardcodear
    // secretos en el repo. El fallback usa auth integrada para entornos (CI Windows / LocalDB).
    // Ejemplo Docker: TEST_DB_CONNECTION="Server=localhost;Database=NexoClean_Test;User Id=sa;Password=***;TrustServerCertificate=True;"
    public static readonly string TestConnection =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Server=localhost;Database=NexoClean_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            // Apunta el DbContext a una base de datos de pruebas dedicada.
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<clsNexoDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<clsNexoDbContext>(o => o.UseSqlServer(TestConnection));

            // Fuerza el esquema de auth de prueba.
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = clsTestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = clsTestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = clsTestAuthHandler.SchemeName;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, clsTestAuthHandler>(clsTestAuthHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>Se conecta a la BD de pruebas ya existente y siembra datos (si hace falta) una
/// sola vez por corrida — NUNCA le administra el esquema.</summary>
public class clsApiFixture : IDisposable
{
    public clsApiFactory Factory { get; }

    /// <summary>Momento en que arrancó la corrida (con un minuto de margen): lo que se creó desde entonces es de esta corrida.</summary>
    private readonly DateTimeOffset _inicioDeLaCorrida = DateTimeOffset.UtcNow.AddMinutes(-1);

    public clsApiFixture()
    {
        Factory = new clsApiFactory();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<clsNexoDbContext>();

        // NUNCA EnsureDeleted() ni Migrate() por defecto acá (S3-HU-11.1, issue #134):
        // TEST_DB_CONNECTION en CI apunta a dbInformesAutomatizados_Pruebas, una base
        // real de la red corporativa (10.10.40.85, ver ci-cd-api.yml) cuyo LOGIN de
        // prueba (userInformesOw) tiene, a propósito, los mismos permisos que la app
        // real necesita para funcionar (leer/escribir filas) — NADA de DDL ni de
        // "owner". Migrate() exige CREATE TABLE/CREATE DATABASE, que ese login no
        // tiene y no debe tener: el esquema de esa base lo administra quien la
        // aprovisiona, no este fixture. Mismo criterio que ya usa clsProgram.cs en la app
        // real: `Database:MigrateOnStartup` apagado por defecto, encendido solo a
        // propósito en los entornos que sí deben crear/actualizar el esquema (docker
        // compose local, por ejemplo). Acá el equivalente es la variable de entorno
        // TEST_DB_MIGRATE_ON_STARTUP — sin ella, se asume que el esquema YA está
        // desplegado y correcto; si no lo está, las pruebas van a fallar con errores
        // de SQL claros (tabla/columna inexistente), no en silencio.
        if (Environment.GetEnvironmentVariable("TEST_DB_MIGRATE_ON_STARTUP") == "true")
            db.Database.Migrate();

        clsDataSeeder.SeedAsync(db).GetAwaiter().GetResult();
    }

    // ---- helpers de clientes HTTP ----
    /// <summary>
    /// <paramref name="userId"/> (issue #199 S4-HU-19.1): simula un usuario Nexo distinto
    /// del de sistema — para probar aislamiento por dueño del recurso con dos usuarios del
    /// mismo rol. Sin especificar, se comporta igual que antes.
    /// </summary>
    public HttpClient Client(string role = "Admin", Guid? userId = null)
    {
        var c = Factory.CreateClient();
        c.DefaultRequestHeaders.Add(clsTestAuthHandler.RoleHeader, role);
        if (userId is { } id) c.DefaultRequestHeaders.Add(clsTestAuthHandler.UserIdHeader, id.ToString());
        return c;
    }

    public HttpClient Anon() => Factory.CreateClient();

    // ---- helpers de datos sembrados ----
    public T WithDb<T>(Func<clsNexoDbContext, T> fn)
    {
        using var scope = Factory.Services.CreateScope();
        return fn(scope.ServiceProvider.GetRequiredService<clsNexoDbContext>());
    }

    public Guid AnyClienteId() => WithDb(db => db.Clientes.First().Id);
    public Guid AnyServicioActivoId() => WithDb(db => db.ServiciosConexos.First(s => s.Activo).Id);

    public void Dispose()
    {
        LimpiarCatalogosHuerfanosDeLaCorrida();
        Factory.Dispose();
    }

    /// <summary>
    /// Al terminar la corrida borra las rutas y configuraciones de vehículo creadas durante ella que ninguna línea ni tarifa
    /// histórica usa. La aplicación solo crea una ruta o un vehículo junto con la línea que lo usa, así que uno creado en esta
    /// corrida y sin ningún uso es un residuo de prueba (varias pruebas arman su catálogo a mano y solo borran la solicitud).
    /// Es de mejor esfuerzo: si la base está ocupada o inaccesible no se rompe el cierre de la corrida.
    /// </summary>
    private void LimpiarCatalogosHuerfanosDeLaCorrida()
    {
        try
        {
            WithDb(db =>
            {
                db.ConfiguracionesVehiculo.Where(v => v.CreatedAt >= _inicioDeLaCorrida
                    && !db.LineasSolicitud.Any(l => l.ConfigVehiculoId == v.Id)
                    && !db.TarifasHistoricas.Any(h => h.ConfigVehiculoId == v.Id)).ExecuteDelete();
                db.Rutas.Where(r => r.CreatedAt >= _inicioDeLaCorrida
                    && !db.LineasSolicitud.Any(l => l.RutaId == r.Id)
                    && !db.TarifasHistoricas.Any(h => h.RutaId == r.Id)).ExecuteDelete();
                return 0;
            });
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException) { }
    }
}

[CollectionDefinition("api")]
public class clsApiCollection : ICollectionFixture<clsApiFixture> { }
