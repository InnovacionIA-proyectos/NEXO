using Nexo.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Infra;

public class ApiFactory : WebApplicationFactory<Program>
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
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<NexoDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<NexoDbContext>(o => o.UseSqlServer(TestConnection));

            // Fuerza el esquema de auth de prueba.
            services.Configure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}

/// <summary>Se conecta a la BD de pruebas ya existente y siembra datos (si hace falta) una
/// sola vez por corrida — NUNCA le administra el esquema.</summary>
public class ApiFixture : IDisposable
{
    public ApiFactory Factory { get; }

    public ApiFixture()
    {
        Factory = new ApiFactory();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();

        // NUNCA EnsureDeleted() ni Migrate() por defecto acá (S3-HU-11.1, issue #134):
        // TEST_DB_CONNECTION en CI apunta a dbInformesAutomatizados_Pruebas, una base
        // real de la red corporativa (10.10.40.85, ver ci-cd-api.yml) cuyo LOGIN de
        // prueba (userInformesOw) tiene, a propósito, los mismos permisos que la app
        // real necesita para funcionar (leer/escribir filas) — NADA de DDL ni de
        // "owner". Migrate() exige CREATE TABLE/CREATE DATABASE, que ese login no
        // tiene y no debe tener: el esquema de esa base lo administra quien la
        // aprovisiona, no este fixture. Mismo criterio que ya usa Program.cs en la app
        // real: `Database:MigrateOnStartup` apagado por defecto, encendido solo a
        // propósito en los entornos que sí deben crear/actualizar el esquema (docker
        // compose local, por ejemplo). Acá el equivalente es la variable de entorno
        // TEST_DB_MIGRATE_ON_STARTUP — sin ella, se asume que el esquema YA está
        // desplegado y correcto; si no lo está, las pruebas van a fallar con errores
        // de SQL claros (tabla/columna inexistente), no en silencio.
        if (Environment.GetEnvironmentVariable("TEST_DB_MIGRATE_ON_STARTUP") == "true")
            db.Database.Migrate();

        DataSeeder.SeedAsync(db).GetAwaiter().GetResult();
    }

    // ---- helpers de clientes HTTP ----
    public HttpClient Client(string role = "Admin")
    {
        var c = Factory.CreateClient();
        c.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return c;
    }

    public HttpClient Anon() => Factory.CreateClient();

    // ---- helpers de datos sembrados ----
    public T WithDb<T>(Func<NexoDbContext, T> fn)
    {
        using var scope = Factory.Services.CreateScope();
        return fn(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    public Guid AnyClienteId() => WithDb(db => db.Clientes.First().Id);
    public Guid AnyServicioActivoId() => WithDb(db => db.ServiciosConexos.First(s => s.Activo).Id);

    public void Dispose() => Factory.Dispose();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
