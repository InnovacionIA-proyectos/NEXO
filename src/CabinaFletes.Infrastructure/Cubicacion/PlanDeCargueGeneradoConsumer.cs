using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Consume el resultado de una cubicación exitosa (issue #28, S2-HU-075) y lo
/// aplica al <c>PlanCargue</c> correspondiente — reactivando una tabla que ya
/// existía en el DbContext sin ningún código que la usara.
///
/// IConsumer&lt;T&gt; real de MassTransit: hereda la política de reintentos (1s/5s/
/// 15s/60s) y el DLQ (`PlanDeCargueGenerado_error`) de la tubería compartida de
/// NEXO (ver MensajeriaDependencyInjection.cs::Tuberia) — no hay código de
/// reintento propio aquí, a diferencia de la primera versión de este puente.
/// La topología (bind al exchange `cubicador.eventos` de Cubicador, no al
/// exchange-por-tipo que MassTransit crearía por defecto) se configura en el
/// registro del bus, no aquí.
/// </summary>
public sealed class PlanDeCargueGeneradoConsumer(CabinaFletesDbContext db) : IConsumer<PlanDeCargueGenerado>
{
    public async Task Consume(ConsumeContext<PlanDeCargueGenerado> context)
    {
        var mensaje = context.Message;
        if (!Guid.TryParse(mensaje.PlanId, out var planId)) return;

        var plan = await db.PlanesCargue.FirstOrDefaultAsync(p => p.Id == planId, context.CancellationToken);
        if (plan is null) return; // el trabajo puede haberse creado y purgado en otro proceso

        plan.Estado = mensaje.Resultado.Estado;
        plan.Puntaje = mensaje.Resultado.Puntaje;
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
