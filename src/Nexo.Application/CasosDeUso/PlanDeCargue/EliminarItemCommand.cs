using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>S3-HU-02 (issue #104): borra un ítem del plan — DELETE
/// /api/planes-cargue/{id}/items/{itemId}.</summary>
public sealed record EliminarItemCommand(Guid PlanCargueId, Guid ItemId) : ICommand<ComandoAceptadoDto>;

public sealed class EliminarItemCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<EliminarItemCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(EliminarItemCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "eliminar_item", planIdTexto, new EliminarItemInput(command.ItemId.ToString()), ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
