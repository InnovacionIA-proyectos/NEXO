using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>Borra un ítem del plan — DELETE
/// /api/planes-cargue/{id}/items/{itemId}.</summary>
public sealed record clsEliminarItemCommand(Guid PlanCargueId, Guid ItemId) : ICommand<clsComandoAceptadoDto>;

public sealed class clsEliminarItemCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsEliminarItemCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsEliminarItemCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "eliminar_item", planIdTexto, new clsEliminarItemInput(command.ItemId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
