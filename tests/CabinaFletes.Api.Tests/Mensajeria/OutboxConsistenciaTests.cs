using CabinaFletes.Api.Tests.Infra;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CabinaFletes.Api.Tests.Mensajeria;

/// <summary>
/// La prueba que justifica el Transactional Outbox.
///
/// Sin Outbox, publicar manda el mensaje al broker de inmediato. Si la transacción
/// falla después, el mensaje ya salió: queda un trabajo encolado apuntando a datos
/// que nunca se guardaron, y el consumidor va a buscar líneas que no existen.
///
/// Con Outbox, publicar escribe una fila en OutboxMessage dentro de la MISMA
/// transacción. La atomicidad la da la base, no un try/catch: si el commit no ocurre,
/// la fila desaparece con todo lo demás y no hay nada que entregar.
///
/// Por eso se prueba contra SQL Server de verdad y no con mocks: lo que se está
/// verificando es precisamente el comportamiento transaccional del motor. Un mock
/// diría que sí a cualquier cosa.
///
/// El transporte va en memoria a propósito. Lo que está bajo prueba es la frontera
/// base-de-datos ↔ tabla de salida; meter RabbitMQ agregaría una dependencia que no
/// cambia lo que se afirma.
/// </summary>
[Trait("Categoria", "Integracion")]
public class OutboxConsistenciaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;

    public async Task InitializeAsync()
    {
        var servicios = new ServiceCollection();

        // Tiempo de comando amplio a propósito: contra una base remota (no local),
        // un comando lento en un momento de carga puede superar los 30 segundos
        // por defecto — y una prueba intermitente enseña al equipo a reintentar en
        // vez de investigar.
        //
        // MISMA base que ApiFixture (S3-HU-11.1, issue #134) — ya NO una base propia
        // ("CabinaFletes_Outbox") separada. Esa separación exigía poder crear bases
        // nuevas a demanda, algo que TEST_DB_CONNECTION en CI (dbInformesAutomatizados_
        // Pruebas, base real de la red corporativa) no permite ni debe permitir: el
        // login de prueba no tiene ni debe tener permisos para crear/administrar
        // bases, solo para leer/escribir filas — igual que la app real. El riesgo de
        // colisión que esa separación evitaba (una colección borrando la base de otra)
        // ya no aplica: ningún fixture hace EnsureDeleted() desde que TEST_DB_CONNECTION
        // puede apuntar a una base real y persistente.
        servicios.AddDbContext<CabinaFletesDbContext>(o =>
            o.UseSqlServer(ApiFactory.TestConnection, sql => sql.CommandTimeout(180)));

        servicios.AddMassTransitTestHarness(x =>
        {
            x.AddEntityFrameworkOutbox<CabinaFletesDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });
        });

        _servicios = servicios.BuildServiceProvider(validateScopes: true);

        using (var alcance = _servicios.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
            // Mismo criterio que ApiFixture (S3-HU-11.1, issue #134): NO migra por
            // defecto contra la base real — el login de prueba no tiene permisos de
            // DDL, y no debe tenerlos. El esquema ya debe estar desplegado ahí.
            if (Environment.GetEnvironmentVariable("TEST_DB_MIGRATE_ON_STARTUP") == "true")
                await db.Database.MigrateAsync();
            // Arranca limpio: otra corrida pudo dejar mensajes sin entregar.
            //
            // ExecuteDeleteAsync en vez de SQL crudo a propósito: un "DELETE FROM
            // OutboxMessage" sin calificar asume que la tabla vive en 'dbo', y dejó de
            // ser cierto cuando CabinaFletesDbContext fijó HasDefaultSchema("dbo_NEXO_prueba")
            // (issue #92). Pasando por el DbSet, EF Core resuelve el esquema real del
            // modelo — no hay nada que hardcodear ni que se pueda desactualizar si el
            // esquema vuelve a cambiar.
            await db.Set<OutboxMessage>().ExecuteDeleteAsync();
        }

        _bus = _servicios.GetRequiredService<ITestHarness>();
        await _bus.Start();
    }

    public async Task DisposeAsync()
    {
        await _bus.Stop();
        await _servicios.DisposeAsync();
    }

    private static AutoCosteoSolicitado Mensaje() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 42, Guid.NewGuid(), DateTimeOffset.UtcNow);

    // Cuenta SOLO la fila de ESTE mensaje (por MessageId), no toda la tabla (S3-HU-11.1,
    // issue #134): desde que esta clase comparte la base real con el resto de la suite
    // (ya no una base "CabinaFletes_Outbox" exclusiva — ver InitializeAsync), CUALQUIER
    // otro test que publique algo por el Outbox real (varios en Api.Tests lo hacen)
    // agrega filas a esta misma tabla. Un COUNT() ciego sobre toda la tabla es exactamente
    // el tipo de aserción que un test paralelo ajeno puede romper sin que el bug sea de
    // este código — filtrar por el MessageId que EL PROPIO test fijó lo hace correcto sin
    // importar qué más haya en la tabla al mismo tiempo.
    private async Task<int> MensajesEnLaTablaDeSalida(Guid messageId)
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        return await db.Set<OutboxMessage>().CountAsync(m => m.MessageId == messageId);
    }

    [Fact]
    public async Task Si_la_transaccion_confirma_el_mensaje_queda_para_entregar()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var messageId = Guid.NewGuid();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje(), ctx => ctx.MessageId = messageId);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        Assert.Equal(1, await MensajesEnLaTablaDeSalida(messageId));
    }

    [Fact]
    public async Task Si_la_transaccion_revierte_no_queda_nada_que_entregar()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var messageId = Guid.NewGuid();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje(), ctx => ctx.MessageId = messageId);
            await db.SaveChangesAsync();

            // El caso real: algo falla después de publicar y antes de confirmar.
            await tx.RollbackAsync();
        }

        Assert.Equal(0, await MensajesEnLaTablaDeSalida(messageId));
        Assert.False(await _bus.Published.Any<AutoCosteoSolicitado>(x => x.Context.MessageId == messageId));
    }

    [Fact]
    public async Task Publicar_sin_confirmar_tampoco_deja_nada()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var messageId = Guid.NewGuid();

        await using (await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje(), ctx => ctx.MessageId = messageId);
            // Ni SaveChanges ni Commit: se abandona el alcance, como haría una excepción.
        }

        Assert.Equal(0, await MensajesEnLaTablaDeSalida(messageId));
    }
}
