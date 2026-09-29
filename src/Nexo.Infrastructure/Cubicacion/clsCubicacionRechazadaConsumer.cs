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
            // Antes: `return` en silencio, sin loguear. Para "optimizar" este Id
            // siempre es un PlanCargue real (ya existe antes de publicar), asi que
            // este caso era casi imposible. Pero "crear_desde_linea"
            // (clsCrearPlanDesdeLineaCommand) publica con PlanId = LineaSolicitudId
            // A PROPOSITO, porque todavia no existe ningun PlanCargue que reportar -
            // asi que CUALQUIER rechazo de ese flujo (preset invalido, error de
            // negocio, lo que sea) cae siempre aca y se perdia sin dejar rastro: el
            // frontend quedaba sondeando GET /por-linea/{lineaId} para siempre, sin
            // poder distinguir "todavia procesando" de "fallo y nadie se entero".
            // Este log al menos lo hace visible para soporte/operacion. Cerrar el
            // circuito hasta el frontend (mostrar el error en la Mesa de Costeo)
            // requiere ademas exponer un estado de rechazo por LineaSolicitudId -
            // hoy clsPlanFlotaItemDto no tiene de donde sacarlo - pendiente como
            // cambio aparte, no incluido en este fix.
            log.LogWarning(
                "Rechazo de cubicacion para Id={Id} sin PlanCargue asociado: probablemente un fallo de " +
                "'crear_desde_linea' (Id = LineaSolicitudId, plan nunca llego a crearse). " +
                "Codigo={Codigo} Mensaje={Mensaje}", planId, mensaje.Codigo, mensaje.Mensaje);
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
