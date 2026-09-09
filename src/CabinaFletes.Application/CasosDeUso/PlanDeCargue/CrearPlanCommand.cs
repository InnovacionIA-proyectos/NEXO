using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Domain.PlanDeCargue.Entidades;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): crea un plan de cargue nuevo — POST /api/planes-cargue. El
/// PlanId lo genera NEXO acá (no Cubicador, ver criterio 1 de la HU): la fila espejo
/// queda en "borrador" de inmediato en NEXO, y el comando "crear_plan" le pide a
/// Cubicador crear la fila REAL con ese mismo id en su propia base. Si Cubicador la
/// rechaza (p.ej. vehiculoId inexistente en su catálogo), <c>CubicacionRechazadaConsumer</c>
/// marca esta misma fila "error" — mismo camino que ya usa "optimizar".
///
/// A propósito NO pasa por <c>IParidadCubicadorPort</c>: es una operación de datos, no de
/// cálculo (HU S3-HU-02, criterio 5) — a diferencia de <see cref="SolicitarOptimizacionCommand"/>.
///
/// Decisión de que el planId lo genera NEXO (no Cubicador): ver
/// docs/decisiones/ADR-planid-generado-por-nexo.md (issue #102, S3-HU-01.2).
/// </summary>
public sealed record CrearPlanCommand(string Nombre, Guid VehiculoId) : ICommand<SolicitudDeCubicacionAceptadaDto>;

public sealed class CrearPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<CrearPlanCommand, SolicitudDeCubicacionAceptadaDto>
{
    public async Task<SolicitudDeCubicacionAceptadaDto> Handle(CrearPlanCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ValidationFailedException("NOMBRE_REQUERIDO", "El nombre del plan es obligatorio.");

        var planId = Guid.NewGuid();
        var plan = new PlanCargue
        {
            Id = planId, Nombre = command.Nombre.Trim(), VehiculoId = command.VehiculoId, Estado = "borrador",
        };

        await planes.PrepararCreacionAsync(plan, ct);

        // Publicar ANTES de guardar (mismo criterio que SolicitarOptimizacionCommand): el
        // Outbox mete el mensaje en la MISMA transacción que la fila nueva.
        await cubicacion.PublicarComandoAsync(
            "crear_plan", planId.ToString(),
            new CrearPlanInput(plan.Nombre, plan.VehiculoId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new SolicitudDeCubicacionAceptadaDto(planId, plan.Estado, $"/api/planes-cargue/{planId}");
    }
}
