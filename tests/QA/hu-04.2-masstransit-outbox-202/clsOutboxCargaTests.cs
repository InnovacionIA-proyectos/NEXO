// Prueba de carga QA para la HU-04.2 (issue #14, PR #61) — NO forma parte del PR
// original. Para reproducirla: copiar este archivo dentro de
// tests/Nexo.Api.Tests/Mensajeria/ (mismas dependencias del proyecto) y
// correr `dotnet test --filter FullyQualifiedName~OutboxCarga` con
// TEST_DB_CONNECTION apuntando a un SQL Server real. Ver README.md de esta
// carpeta para el resultado ya obtenido y el detalle completo.
using Nexo.Api.Tests.Infra;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Solicitudes;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Nexo.Api.Tests.Mensajeria;

/// <summary>
/// Confirma que el Outbox transaccional aguanta publicaciones concurrentes sin
/// perder mensajes, sin deadlocks y sin duplicados — antes de que este mecanismo
/// empiece a recibir
/// tráfico real de cargas masivas de pliegos.
/// </summary>
[Trait("Categoria", "Carga")]
public class clsOutboxCargaTests : IAsyncLifetime
{
    private ServiceProvider _servicios = null!;
    private ITestHarness _bus = null!;

    public async Task InitializeAsync()
    {
        var servicios = new ServiceCollection();
        servicios.AddDbContext<clsNexoDbContext>(o => o.UseSqlServer(clsApiFactory.TestConnection));
        servicios.AddMassTransitTestHarness(x =>
        {
            x.AddEntityFrameworkOutbox<clsNexoDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });
        });
        _servicios = servicios.BuildServiceProvider(validateScopes: true);

        using var alcance = _servicios.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<clsNexoDbContext>();
        await db.Database.MigrateAsync();
        // ExecuteDeleteAsync en vez de SQL crudo: un "DELETE FROM OutboxMessage" sin
        // calificar asume esquema 'dbo', y dejó de ser cierto desde que
        // clsNexoDbContext fijó HasDefaultSchema("db_NEXO") (issue #92).
        await db.Set<OutboxMessage>().ExecuteDeleteAsync();

        _bus = _servicios.GetRequiredService<ITestHarness>();
        await _bus.Start();
    }

    public async Task DisposeAsync()
    {
        await _bus.Stop();
        await _servicios.DisposeAsync();
    }

    [Fact]
    public async Task Publicaciones_concurrentes_no_pierden_ni_duplican_mensajes()
    {
        const int concurrencia = 30;
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        var tareas = Enumerable.Range(0, concurrencia).Select(async _ =>
        {
            using var alcance = _servicios.CreateScope();
            var db = alcance.ServiceProvider.GetRequiredService<clsNexoDbContext>();
            var publicador = alcance.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            await using var tx = await db.Database.BeginTransactionAsync();
            await publicador.Publish(new clsAutoCosteoSolicitado(
                Guid.NewGuid(), Guid.NewGuid(), 10, Guid.NewGuid(), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        });

        await Task.WhenAll(tareas);
        cronometro.Stop();

        using var verificacion = _servicios.CreateScope();
        var dbVerificacion = verificacion.ServiceProvider.GetRequiredService<clsNexoDbContext>();
        var enTablaDeSalida = await dbVerificacion.Set<OutboxMessage>().CountAsync();

        Assert.Equal(concurrencia, enTablaDeSalida);

        // No es una aserción de rendimiento estricta (esta suite corre en un
        // contenedor compartido, no en el hardware de producción) - es una
        // guarda contra un deadlock silencioso que colgara la prueba.
        Assert.True(cronometro.ElapsedMilliseconds < 30_000,
            $"{concurrencia} publicaciones concurrentes tardaron {cronometro.ElapsedMilliseconds} ms - " +
            "sospechoso de contención/deadlock en la tabla de salida.");
    }
}
