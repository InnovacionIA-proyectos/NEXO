using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): borra un plan de cargue — DELETE /api/planes-cargue/{id}. El
/// borrado en NEXO es OPTIMISTA (se aplica antes de publicar, no se deshace si Cubicador
/// rechaza el comando): es la misma asimetría del PATCH síncrono que reemplaza — deshacer
/// un borrado es un caso raro y de bajo valor frente a la simplicidad de no reconciliarlo
/// (ver issue #108 para el detalle de esta decisión).
/// </summary>
public sealed record EliminarPlanCommand(Guid PlanCargueId) : ICommand<ComandoAceptadoDto>;

public sealed class EliminarPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<EliminarPlanCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(EliminarPlanCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        await planes.EliminarSiExisteAsync(command.PlanCargueId, ct);

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync("eliminar_plan", planIdTexto, new EliminarPlanInput(), ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
