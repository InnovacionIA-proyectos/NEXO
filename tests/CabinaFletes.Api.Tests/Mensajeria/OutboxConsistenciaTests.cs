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

        servicios.AddDbContext<CabinaFletesDbContext>(o => o.UseSqlServer(ApiFactory.TestConnection));

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
            await db.Database.MigrateAsync();
            // Arranca limpio: otra corrida pudo dejar mensajes sin entregar.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM OutboxMessage");
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

    private async Task<int> MensajesEnLaTablaDeSalida()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        return await db.Set<OutboxMessage>().CountAsync();
    }

    [Fact]
    public async Task Si_la_transaccion_confirma_el_mensaje_queda_para_entregar()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje());
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        Assert.Equal(1, await MensajesEnLaTablaDeSalida());
    }

    [Fact]
    public async Task Si_la_transaccion_revierte_no_queda_nada_que_entregar()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje());
            await db.SaveChangesAsync();

            // El caso real: algo falla después de publicar y antes de confirmar.
            await tx.RollbackAsync();
        }

        Assert.Equal(0, await MensajesEnLaTablaDeSalida());
        Assert.False(await _bus.Published.Any<AutoCosteoSolicitado>());
    }

    [Fact]
    public async Task Publicar_sin_confirmar_tampoco_deja_nada()
    {
        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<CabinaFletesDbContext>();
        var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await using (await db.Database.BeginTransactionAsync())
        {
            await publicador.Publish(Mensaje());
            // Ni SaveChanges ni Commit: se abandona el alcance, como haría una excepción.
        }

        Assert.Equal(0, await MensajesEnLaTablaDeSalida());
    }
}
