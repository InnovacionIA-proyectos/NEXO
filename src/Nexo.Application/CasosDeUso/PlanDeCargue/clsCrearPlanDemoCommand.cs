using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Escenario de demostración (POST /api/planes-cargue/demo). Cubicador elige el vehículo, que NEXO no conoce de antemano: no se genera
/// PlanId real ni fila espejo, solo un CorrelationId de seguimiento; el plan se sigue por SignalR.
/// </summary>
public sealed record clsCrearPlanDemoCommand : ICommand<clsComandoAceptadoDto>;

public sealed class clsCrearPlanDemoCommandHandler(ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsCrearPlanDemoCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsCrearPlanDemoCommand command, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();

        await cubicacion.PublicarComandoAsync("crear_plan_demo", correlationId.ToString(), new clsCrearPlanDemoInput(), ct);

        // El SaveChangesAsync es obligatorio aunque este comando no toque ninguna fila de
        // NEXO: es lo que dispara el flush del outbox transaccional (ver ICubicacionPort).
        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(correlationId, "procesando");
    }
}
