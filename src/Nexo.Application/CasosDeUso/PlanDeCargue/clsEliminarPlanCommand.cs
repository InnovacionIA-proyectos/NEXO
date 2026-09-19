using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Borra un plan de cargue — DELETE /api/planes-cargue/{id}. El
/// borrado en NEXO es OPTIMISTA (se aplica antes de publicar, no se deshace si Cubicador
/// rechaza el comando): es la misma asimetría del PATCH síncrono que reemplaza — deshacer
/// un borrado es un caso raro y de bajo valor frente a la simplicidad de no reconciliarlo.
/// </summary>
public sealed record clsEliminarPlanCommand(Guid PlanCargueId) : ICommand<clsComandoAceptadoDto>;

public sealed class clsEliminarPlanCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsEliminarPlanCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsEliminarPlanCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        await planes.EliminarSiExisteAsync(command.PlanCargueId, ct);

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync("eliminar_plan", planIdTexto, new clsEliminarPlanInput(), ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
