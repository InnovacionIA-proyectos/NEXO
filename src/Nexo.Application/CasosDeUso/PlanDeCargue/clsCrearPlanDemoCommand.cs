using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Escenario de demostración — POST /api/planes-cargue/demo.
/// Cubicador elige internamente el vehículo (preset "13.6 m") y arma la carga mixta con
/// rechazos diseñados (ver cubicador/application/plan_cargue/demo.py) — NEXO no conoce
/// ese vehiculoId de antemano (vive solo en el catálogo de Cubicador), así que a
/// diferencia de <see cref="clsCrearPlanCommand"/> acá NO se genera un PlanId real ni se deja
/// fila espejo en NEXO: solo un CorrelationId de seguimiento (limitación conocida — el
/// plan resultante se sigue por SignalR una vez Cubicador lo confirma, no por GET
/// /api/planes-cargue/{id} en este alcance).
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
