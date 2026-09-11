using CabinaFletes.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Infra;

public class ApiFactory : WebApplicationFactory<Program>
{
    // Cadena de la BD de pruebas. Parametrizable por la variable de entorno TEST_DB_CONNECTION
    // (igual criterio que el proyecto original): así se puede usar auth SQL contra un SQL
    // Server en Docker/Linux —que no acepta Trusted_Connection (auth Windows)— sin hardcodear
    // secretos en el repo. El fallback usa auth integrada para entornos (CI Windows / LocalDB).
    // Ejemplo Docker: TEST_DB_CONNECTION="Server=localhost;Database=CabinaFletesClean_Test;User Id=sa;Password=***;TrustServerCertificate=True;"
    public static readonly string TestConnection =
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Server=localhost;Database=CabinaFletesClean_Test;Trusted_Connection=True;TrustServerCertificate=True;";

    /// <summary>
    /// La misma cadena apuntando a OTRA base.
    ///
    /// Hace falta porque xUnit corre las colecciones en paralelo y varias suites usaban
    /// la misma base: en su momento (cuando <see cref="ApiFixture"/> todavía hacía
    /// EnsureDeleted al arrancar, retirado en S3-HU-11.1/#134 porque TEST_DB_CONNECTION
    /// en CI apunta a una base real y persistente, no descartable) una colección se la
    /// borraba en plena migración a las pruebas del outbox. El síntoma era "A severe
    /// error occurred on the current command", que no dice nada del choque. El riesgo de
    /// colisión sigue existiendo sin el borrado — dos colecciones insertando la misma
    /// fila única en la misma base igual chocan — por eso esta separación por base sigue
    /// haciendo falta.
    ///
    /// Estuvo escondido varias sesiones porque las suites se corrían por separado y con
    /// filtros. Cada grupo con su base: compartirla no aporta nada y sí acopla.
    /// </summary>
    public static string ConexionA(string baseDeDatos)
    {
        var constructor = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(TestConnection)
        {
            InitialCatalog = baseDeDatos
        };
        return constructor.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            // Apunta el DbContext a una base de datos de pruebas dedicada.
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CabinaFletesDbContext>));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddDbContext<CabinaFletesDbContext>(o => o.UseSqlServer(TestConnection));

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

/// <summary>Crea la BD de pruebas (limpia), aplica migraciones y siembra datos una sola vez por corrida.</summary>
public class ApiFixture : IDisposable
{
    public ApiFactory Factory { get; }

    public ApiFixture()
    {
        Factory = new ApiFactory();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        // NUNCA EnsureDeleted() acá (S3-HU-11.1, issue #134): en CI, TEST_DB_CONNECTION
        // apunta a dbInformesAutomatizados_Pruebas, una base de pruebas dedicada pero
        // REAL y persistente en la red corporativa (10.10.40.85, ver ci-cd-api.yml) —
        // no un contenedor descartable. Borrar la base entera para "empezar limpio" no
        // es aceptable ahí bajo ninguna circunstancia. Migrate() es aditivo (crea lo que
        // falte, no toca lo que ya hay) y DataSeeder.SeedAsync ya es idempotente (no-op
        // si ya hay datos) — la combinación es segura para correr repetidas veces contra
        // la MISMA base persistente.
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
    public T WithDb<T>(Func<CabinaFletesDbContext, T> fn)
    {
        using var scope = Factory.Services.CreateScope();
        return fn(scope.ServiceProvider.GetRequiredService<CabinaFletesDbContext>());
    }

    public Guid AnyClienteId() => WithDb(db => db.Clientes.First().Id);
    public Guid AnyServicioActivoId() => WithDb(db => db.ServiciosConexos.First(s => s.Activo).Id);

    public void Dispose() => Factory.Dispose();
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFixture> { }
