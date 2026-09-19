using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CabinaFletes.Infrastructure.Cubicacion;

/// <summary>
/// Consume el rechazo de una cubicación (issue #28, S2-HU-075) y marca el
/// <c>PlanCargue</c> correspondiente como <c>error</c> (S2-HU-08.1: aviso claro, el
/// usuario puede reintentar desde el estado "error"). Ver
/// <see cref="PlanDeCargueGeneradoConsumer"/> para el porqué de la topología.
/// </summary>
public sealed class CubicacionRechazadaConsumer(
    CabinaFletesDbContext db, INotificadorDePlanCargue avisos, ILogger<CubicacionRechazadaConsumer> log)
    : IConsumer<CubicacionRechazada>
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

        try
        {
            await avisos.AvisarAsync(
                new AvisoPlanCargueDto(plan.Id, plan.Estado, plan.Puntaje, mensaje.Mensaje), context.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el rechazo del plan de cargue {PlanCargueId}. El plan ya quedó " +
                "marcado en error; se puede consultar en /api/planes-cargue/{PlanCargueId}.", plan.Id, plan.Id);
        }
    }
}
