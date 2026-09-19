using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// S3-HU-03 (issue #110/#111): consume vehiculo.creado/vehiculo.actualizado y mantiene
/// sincronizado el catálogo de vehículos en la proyección de NEXO
/// (<c>VehiculosCubicacion</c>) — sin esto, GET /api/vehiculos (issue #112) tendría que
/// llamar a Cubicador en caliente, justo lo que la HU prohíbe.
///
/// Una sola clase para los 2 tipos (en vez de dos consumers casi idénticos): el upsert es
/// exactamente el mismo, "creado" vs "actualizado" solo importa para la topología/routing
/// key de Cubicador (ver worker/main.py::_ejecutar_solicitud, tipo "guardar_vehiculo") —
/// del lado de NEXO no hay ninguna diferencia de tratamiento, es upsert por Id en los dos
/// casos (idempotente: un reintento tras un fallo a mitad de camino no duplica nada).
///
/// IConsumer&lt;T&gt; real de MassTransit: hereda la misma tubería de reintentos/DLQ/alerta
/// que <see cref="clsPlanDeCargueGeneradoConsumer"/> (ver clsMensajeriaDependencyInjection.cs).
/// NO hace ninguna llamada saliente a Cubicador (criterio de cierre de S3-HU-03,
/// issue #111): todo lo que escribe viene del propio evento.
/// </summary>
public sealed class clsVehiculoCatalogoConsumer(clsNexoDbContext db)
    : IConsumer<clsVehiculoCreado>, IConsumer<clsVehiculoActualizado>, IConsumer<clsVehiculoEliminado>
{
    public async Task Consume(ConsumeContext<clsVehiculoCreado> context)
    {
        await clsVehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<clsVehiculoActualizado> context)
    {
        await clsVehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    /// <summary>
    /// Cierra el trío del catálogo: sin esto la proyección se quedaría con vehículos que
    /// ya no existen en Cubicador y <c>GET /api/vehiculos</c> los seguiría ofreciendo.
    ///
    /// Borrar lo que no está NO es un error: si el vehículo ya no está en la proyección
    /// (evento duplicado, o la proyección se reconstruyó después del borrado) el
    /// resultado deseado ya se cumplió. Dejar subir una excepción ahí mandaría a
    /// reintentos y DLQ un mensaje que no tiene nada que arreglar — mismo criterio que
    /// el upsert de arriba, que también es idempotente por construcción.
    /// </summary>
    public async Task Consume(ConsumeContext<clsVehiculoEliminado> context)
    {
        var id = Guid.Parse(context.Message.VehiculoId);
        var vehiculo = await db.VehiculosCubicacion.FirstOrDefaultAsync(v => v.Id == id, context.CancellationToken);
        if (vehiculo is null)
            return;

        db.VehiculosCubicacion.Remove(vehiculo);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
