using Nexo.Domain.Cubicacion.Entidades;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// S5-HU-18.5/#277: consume carroceria.creada/carroceria.actualizada/carroceria.eliminada
/// y mantiene sincronizada la proyección de NEXO (<c>TiposCarroceriaCatalogo</c>). Mismo
/// criterio que <see cref="clsMercanciaCatalogoConsumer"/>/<see cref="clsVehiculoCatalogoConsumer"/>.
/// </summary>
public sealed class clsCarroceriaCatalogoConsumer(clsNexoDbContext db)
    : IConsumer<CarroceriaCreada>, IConsumer<CarroceriaActualizada>, IConsumer<CarroceriaEliminada>
{
    public async Task Consume(ConsumeContext<CarroceriaCreada> context)
    {
        await UpsertAsync(db, context.Message.Carroceria, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CarroceriaActualizada> context)
    {
        await UpsertAsync(db, context.Message.Carroceria, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<CarroceriaEliminada> context)
    {
        var carroceria = await db.TiposCarroceriaCatalogo.FirstOrDefaultAsync(c => c.Id == context.Message.CarroceriaId, context.CancellationToken);
        if (carroceria is null)
            return;

        db.TiposCarroceriaCatalogo.Remove(carroceria);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    private static async Task UpsertAsync(clsNexoDbContext db, clsTipoCarroceriaCatalogoEvento e, CancellationToken ct)
    {
        var carroceria = await db.TiposCarroceriaCatalogo.FirstOrDefaultAsync(c => c.Id == e.Id, ct);
        if (carroceria is null)
        {
            carroceria = new clsTipoCarroceriaCatalogo { Id = e.Id };
            db.TiposCarroceriaCatalogo.Add(carroceria);
        }

        carroceria.CodigoCarroceria = e.CodigoCarroceria;
        carroceria.NombreCarroceria = e.NombreCarroceria;
        carroceria.Activo = e.Activo;
        carroceria.Version = e.Version;
        carroceria.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
