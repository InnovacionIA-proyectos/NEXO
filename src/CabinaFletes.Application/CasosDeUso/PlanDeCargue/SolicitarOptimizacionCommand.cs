using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Pide la optimización de un plan de cargue ya existente (S2-HU-08.1). NO bloquea: dejar
/// el plan en "procesando" y publicar a la cola es rápido — el motor corre aparte y el
/// resultado llega por cola (consumers de Infrastructure: PlanDeCargueGeneradoConsumer /
/// CubicacionRechazadaConsumer).
///
/// Cerrada la compuerta de paridad (S2-HU-08.2, #30), esto ni siquiera llega a tocar el
/// plan ni a publicar nada: 503 "en verificación" antes de cualquier efecto.
/// </summary>
public sealed record SolicitarOptimizacionCommand(
    Guid PlanCargueId, string? Estrategia, string? Algoritmo, int? Semilla, bool? RespetarBloqueadas)
    : ICommand<SolicitudDeCubicacionAceptadaDto>;

public sealed class SolicitarOptimizacionCommandHandler(
    IParidadCubicadorPort paridad,
    IPlanCargueRepository planes,
    ICubicacionPort cubicacion,
    IUnitOfWork uow)
    : ICommandHandler<SolicitarOptimizacionCommand, SolicitudDeCubicacionAceptadaDto>
{
    public async Task<SolicitudDeCubicacionAceptadaDto> Handle(
        SolicitarOptimizacionCommand command, CancellationToken ct = default)
    {
        if (!paridad.Habilitada)
            throw new ServiceUnavailableException(
                "CUBICACION_EN_VERIFICACION",
                "El plan de cargue está en verificación: el motor todavía no alcanza la paridad " +
                "requerida (26/26). No se entregan planes sin ese respaldo.");

        var plan = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (plan.Estado == "procesando")
            throw new ConflictException("PLAN_CARGUE_EN_PROCESO", "Este plan ya tiene una optimización en curso.");

        plan.Estado = "procesando";

        // Publicar ANTES de guardar: el Outbox mete el mensaje en la MISMA transacción
        // que el cambio de estado. Al revés, confirmado en runtime (ver
        // docs/mensajeria/CUBICACION.md #5), el mensaje no se entrega.
        await cubicacion.SolicitarCubicacionAsync(
            plan.Id,
            command.Estrategia ?? plan.Estrategia,
            command.Algoritmo ?? plan.Algoritmo,
            command.Semilla ?? plan.Semilla,
            command.RespetarBloqueadas ?? false,
            ct);

        await uow.SaveChangesAsync(ct);

        return new SolicitudDeCubicacionAceptadaDto(plan.Id, plan.Estado, $"/api/planes-cargue/{plan.Id}");
    }
}
