using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): escenario de demostración — POST /api/planes-cargue/demo.
/// Cubicador elige internamente el vehículo (preset "13.6 m") y arma la carga mixta con
/// rechazos diseñados (ver cubicador/application/plan_cargue/demo.py) — NEXO no conoce
/// ese vehiculoId de antemano (vive solo en el catálogo de Cubicador), así que a
/// diferencia de <see cref="CrearPlanCommand"/> acá NO se genera un PlanId real ni se deja
/// fila espejo en NEXO: solo un CorrelationId de seguimiento (ver issue #108, limitación
/// documentada — el plan resultante se sigue por SignalR una vez Cubicador lo confirma,
/// no por GET /api/planes-cargue/{id} en este alcance).
/// </summary>
public sealed record CrearPlanDemoCommand : ICommand<ComandoAceptadoDto>;

public sealed class CrearPlanDemoCommandHandler(ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<CrearPlanDemoCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(CrearPlanDemoCommand command, CancellationToken ct = default)
    {
        var correlationId = Guid.NewGuid();

        await cubicacion.PublicarComandoAsync("crear_plan_demo", correlationId.ToString(), new CrearPlanDemoInput(), ct);

        // El SaveChangesAsync es obligatorio aunque este comando no toque ninguna fila de
        // NEXO: es lo que dispara el flush del outbox transaccional (ver ICubicacionPort).
        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(correlationId, "procesando");
    }
}
