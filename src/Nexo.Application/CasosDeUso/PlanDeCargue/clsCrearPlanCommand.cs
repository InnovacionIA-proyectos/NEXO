using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Domain.PlanDeCargue.Entidades;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Crea un plan de cargue (POST /api/planes-cargue). El PlanId lo genera NEXO: la fila espejo queda de inmediato y "crear_plan" le pide a
/// Cubicador crear la fila real con ese id; si la rechaza, <c>clsCubicacionRechazadaConsumer</c> la marca "error".
/// La fila nace en "procesando" (no "borrador") para que la guarda de <see cref="clsSolicitarOptimizacionCommand"/> impida optimizar antes de
/// que "crear_plan" se procese (misma cola: "optimizar" podía atenderse primero y fallar con PLAN_NO_ENCONTRADO).
/// No pasa por <c>IParidadCubicadorPort</c>: es una operación de datos, no de cálculo.
/// </summary>
public sealed record clsCrearPlanCommand(string Nombre, Guid VehiculoId) : ICommand<clsSolicitudDeCubicacionAceptadaDto>;

public sealed class clsCrearPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsCrearPlanCommand, clsSolicitudDeCubicacionAceptadaDto>
{
    public async Task<clsSolicitudDeCubicacionAceptadaDto> Handle(clsCrearPlanCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new clsValidationFailedException("NOMBRE_REQUERIDO", "El nombre del plan es obligatorio.");

        var planId = Guid.NewGuid();
        var plan = new clsPlanCargue
        {
            Id = planId, Nombre = command.Nombre.Trim(), VehiculoId = command.VehiculoId, Estado = "procesando",
        };

        await planes.PrepararCreacionAsync(plan, ct);

        // Publicar ANTES de guardar (mismo criterio que clsSolicitarOptimizacionCommand): el
        // Outbox mete el mensaje en la MISMA transacción que la fila nueva.
        await cubicacion.PublicarComandoAsync(
            "crear_plan", planId.ToString(),
            new clsCrearPlanInput(plan.Nombre, plan.VehiculoId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new clsSolicitudDeCubicacionAceptadaDto(planId, plan.Estado, $"/api/planes-cargue/{planId}");
    }
}
