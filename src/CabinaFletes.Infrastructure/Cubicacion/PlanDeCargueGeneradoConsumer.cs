using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Infrastructure.Persistence;
using CabinaFletes.Mensajes.Cubicacion;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
public sealed class PlanDeCargueGeneradoConsumer(
    CabinaFletesDbContext db, INotificadorDePlanCargue avisos, ILogger<PlanDeCargueGeneradoConsumer> log)
    : IConsumer<PlanDeCargueGenerado>
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

        // Un fallo al avisar NO es un fallo del trabajo (S2-HU-08.1): la verdad sigue en
        // GET /api/planes-cargue/{id}. Dejar que la excepción suba mandaría este mensaje
        // — que ya cumplió su parte, el plan quedó guardado — a reintentos y DLQ por algo
        // tan ajeno como un navegador desconectado. Mismo criterio que AvisarTrabajoConsumer.
        try
        {
            await avisos.AvisarAsync(
                new AvisoPlanCargueDto(plan.Id, plan.Estado, plan.Puntaje), context.CancellationToken);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex,
                "No se pudo avisar el desenlace del plan de cargue {PlanCargueId}. El plan ya quedó " +
                "guardado; se puede consultar en /api/planes-cargue/{PlanCargueId}.", plan.Id, plan.Id);
        }
    }
}
