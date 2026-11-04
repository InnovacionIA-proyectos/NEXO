using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Infrastructure.Persistence;
using Nexo.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexo.Infrastructure.Cubicacion;

/// <summary>Marca el plan como <c>error</c> al recibir el rechazo de una cubicación (el usuario puede reintentar).</summary>
public sealed class clsCubicacionRechazadaConsumer(
    clsNexoDbContext db, INotificadorDePlanCargue avisos, IRechazoCubicacionLineaPort rechazos,
    ILogger<clsCubicacionRechazadaConsumer> log)
    : IConsumer<CubicacionRechazada>
{
    public async Task Consume(ConsumeContext<CubicacionRechazada> context)
    {
        var mensaje = context.Message;
        if (!Guid.TryParse(mensaje.PlanId, out var planId))
        {
            log.LogWarning(
                "Rechazo de cubicacion con PlanId no parseable como Guid ({PlanId}); se descarta sin marcar nada. " +
                "Codigo={Codigo} Mensaje={Mensaje}", mensaje.PlanId, mensaje.Codigo, mensaje.Mensaje);
            return;
        }

        var plan = await db.PlanesCargue.FirstOrDefaultAsync(p => p.Id == planId, context.CancellationToken);
        if (plan is null)
        {
            // El Id puede no ser un PlanCargue: crear_desde_linea publica con PlanId = LineaSolicitudId porque aún no existe plan.
            // Se loguea para que ese rechazo no se pierda sin rastro.
            log.LogWarning(
                "Rechazo de cubicacion para Id={Id} sin PlanCargue asociado: probablemente un fallo de " +
                "'crear_desde_linea' (Id = LineaSolicitudId, plan nunca llego a crearse). " +
                "Codigo={Codigo} Mensaje={Mensaje}", planId, mensaje.Codigo, mensaje.Mensaje);

            // Solo si el Id ES una línea real se guarda el motivo por línea; cualquier otro Id huérfano sigue siendo solo un log.
            if (await rechazos.RegistrarAsync(planId, mensaje.Codigo, mensaje.Mensaje, context.CancellationToken))
                await db.SaveChangesAsync(context.CancellationToken);
            return;
        }

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
