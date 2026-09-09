using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using MassTransit;

namespace CabinaFletes.Infrastructure.Cubicacion;

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
/// que <see cref="PlanDeCargueGeneradoConsumer"/> (ver MensajeriaDependencyInjection.cs).
/// NO hace ninguna llamada saliente a Cubicador (criterio de cierre de S3-HU-03,
/// issue #111): todo lo que escribe viene del propio evento.
/// </summary>
public sealed class VehiculoCatalogoConsumer(CabinaFletesDbContext db)
    : IConsumer<VehiculoCreado>, IConsumer<VehiculoActualizado>
{
    public async Task Consume(ConsumeContext<VehiculoCreado> context)
    {
        await VehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<VehiculoActualizado> context)
    {
        await VehiculoProyeccion.UpsertAsync(db, context.Message.Vehiculo, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
