using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>Mantiene la proyección de vehículos (VehiculosCubicacion) desde vehiculo.creado/actualizado: upsert idempotente por Id,
/// igual para ambos tipos. No consulta a Cubicador; reintentos y DLQ los da la tubería compartida.</summary>
public sealed class clsVehiculoCatalogoConsumer(clsNexoDbContext db)
    : IConsumer<VehiculoCreado>, IConsumer<VehiculoActualizado>, IConsumer<VehiculoEliminado>
{
    public async Task Consume(ConsumeContext<VehiculoCreado> context)
    {
        await clsVehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<VehiculoActualizado> context)
    {
        await clsVehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    /// <summary>Quita el vehículo de la proyección. Borrar lo que no está no es un error (evento duplicado o proyección reconstruida):
    /// subir una excepción mandaría a reintentos y DLQ un mensaje sin nada que arreglar.</summary>
    public async Task Consume(ConsumeContext<VehiculoEliminado> context)
    {
        var id = Guid.Parse(context.Message.VehiculoId);
        var vehiculo = await db.VehiculosCubicacion.FirstOrDefaultAsync(v => v.Id == id, context.CancellationToken);
        if (vehiculo is null)
            return;

        db.VehiculosCubicacion.Remove(vehiculo);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
