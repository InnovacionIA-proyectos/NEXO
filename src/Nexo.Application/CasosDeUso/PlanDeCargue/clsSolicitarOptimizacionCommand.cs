using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Pide la optimización de un plan ya existente. NO bloquea: deja el plan en "procesando" y publica a la cola; el resultado llega por los consumers.
/// Con la compuerta de paridad cerrada responde 503 "en verificación" antes de cualquier efecto.
/// </summary>
public sealed record clsSolicitarOptimizacionCommand(
    Guid PlanCargueId, string? Estrategia, string? Algoritmo, int? Semilla, bool? RespetarBloqueadas)
    : ICommand<clsSolicitudDeCubicacionAceptadaDto>;

public sealed class clsSolicitarOptimizacionCommandHandler(
    IParidadCubicadorPort paridad,
    IPlanCargueRepository planes,
    ICubicacionPort cubicacion,
    IUnitOfWork uow)
    : ICommandHandler<clsSolicitarOptimizacionCommand, clsSolicitudDeCubicacionAceptadaDto>
{
    public async Task<clsSolicitudDeCubicacionAceptadaDto> Handle(
        clsSolicitarOptimizacionCommand command, CancellationToken ct = default)
    {
        if (!paridad.Habilitada)
            throw new clsServiceUnavailableException(
                "CUBICACION_EN_VERIFICACION",
                "El plan de cargue está en verificación: el motor todavía no alcanza la paridad " +
                "requerida (26/26). No se entregan planes sin ese respaldo.");

        var plan = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (plan.Estado == "procesando")
            throw new clsConflictException("PLAN_CARGUE_EN_PROCESO", "Este plan ya tiene una optimización en curso.");

        plan.Estado = "procesando";

        // Publicar ANTES de guardar: así el Outbox mete el mensaje en la MISMA transacción que el cambio de estado; al revés el mensaje no se entrega.
        await cubicacion.SolicitarCubicacionAsync(
            plan.Id,
            command.Estrategia ?? plan.Estrategia,
            command.Algoritmo ?? plan.Algoritmo,
            command.Semilla ?? plan.Semilla,
            command.RespetarBloqueadas ?? false,
            ct);

        await uow.SaveChangesAsync(ct);

        return new clsSolicitudDeCubicacionAceptadaDto(plan.Id, plan.Estado, $"/api/planes-cargue/{plan.Id}");
    }
}
