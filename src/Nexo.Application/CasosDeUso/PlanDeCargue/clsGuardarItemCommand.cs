using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Alta/edición de un ítem del plan — POST
/// /api/planes-cargue/{id}/items. <c>Id</c> nulo = alta; con <c>Id</c> = edición (upsert),
/// misma semántica que el POST síncrono de Cubicador que este comando reemplaza.
/// </summary>
public sealed record clsGuardarItemCommand(
    Guid PlanCargueId,
    Guid? Id,
    string? Sku,
    string Descripcion,
    string? Pedido,
    string? clsCliente,
    string? Destino,
    int SecuenciaEntrega,
    decimal LargoMm,
    decimal AnchoMm,
    decimal AltoMm,
    decimal PesoKg,
    int Cantidad,
    string? Color,
    int Prioridad,
    string? TipoEmbalaje,
    bool Apilable,
    int MaxNiveles,
    decimal MaxPesoEncimaKg,
    bool Rotable,
    bool MantenerVertical,
    bool Fragil,
    bool Peligroso,
    bool Refrigerado,
    string? GrupoCompatibilidad,
    string? Observaciones)
    : ICommand<clsComandoAceptadoDto>;

public sealed class clsGuardarItemCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsGuardarItemCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsGuardarItemCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (string.IsNullOrWhiteSpace(command.Descripcion))
            throw new clsValidationFailedException("DESCRIPCION_REQUERIDA", "La descripción del ítem es obligatoria.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "guardar_item", planIdTexto,
            new clsGuardarItemInput(
                command.Id?.ToString(), command.Sku, command.Descripcion.Trim(), command.Pedido, command.clsCliente,
                command.Destino, command.SecuenciaEntrega, command.LargoMm, command.AnchoMm, command.AltoMm,
                command.PesoKg, command.Cantidad, command.Color, command.Prioridad, command.TipoEmbalaje,
                command.Apilable, command.MaxNiveles, command.MaxPesoEncimaKg, command.Rotable,
                command.MantenerVertical, command.Fragil, command.Peligroso, command.Refrigerado,
                command.GrupoCompatibilidad, command.Observaciones),
            ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
