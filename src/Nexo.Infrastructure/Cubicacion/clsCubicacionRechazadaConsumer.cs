using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>
/// Consume el rechazo de una cubicación y marca el
/// <c>clsPlanCargue</c> correspondiente como <c>error</c> (aviso claro: el
/// usuario puede reintentar desde el estado "error"). Ver
/// <see cref="clsPlanDeCargueGeneradoConsumer"/> para el porqué de la topología.
/// </summary>
public sealed class clsCubicacionRechazadaConsumer(
    clsNexoDbContext db, INotificadorDePlanCargue avisos, ILogger<clsCubicacionRechazadaConsumer> log)
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
                new clsAvisoPlanCargueDto(plan.Id, plan.Estado, plan.Puntaje, mensaje.Mensaje), context.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el rechazo del plan de cargue {PlanCargueId}. El plan ya quedó " +
                "marcado en error; se puede consultar en /api/planes-cargue/{PlanCargueId}.", plan.Id, plan.Id);
        }
    }
}
