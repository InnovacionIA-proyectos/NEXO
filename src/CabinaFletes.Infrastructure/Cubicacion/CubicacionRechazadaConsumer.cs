using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Consume el rechazo de una cubicación (issue #28, S2-HU-075) y marca el
/// <c>PlanCargue</c> correspondiente como <c>error</c>. Ver
/// <see cref="PlanDeCargueGeneradoConsumer"/> para el porqué de la topología.
/// </summary>
public sealed class CubicacionRechazadaConsumer(CabinaFletesDbContext db) : IConsumer<CubicacionRechazada>
{
    public async Task Consume(ConsumeContext<CubicacionRechazada> context)
    {
        var mensaje = context.Message;
        if (!Guid.TryParse(mensaje.PlanId, out var planId)) return;

        var plan = await db.PlanesCargue.FirstOrDefaultAsync(p => p.Id == planId, context.CancellationToken);
        if (plan is null) return;

        plan.Estado = "error";
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
