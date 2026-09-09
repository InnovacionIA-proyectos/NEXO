using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): alta/edición de un ítem del plan — POST
/// /api/planes-cargue/{id}/items. <c>Id</c> nulo = alta; con <c>Id</c> = edición (upsert),
/// misma semántica que el POST síncrono de Cubicador que este comando reemplaza.
/// </summary>
public sealed record GuardarItemCommand(
    Guid PlanCargueId,
    Guid? Id,
    string? Sku,
    string Descripcion,
    string? Pedido,
    string? Cliente,
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
    : ICommand<ComandoAceptadoDto>;

public sealed class GuardarItemCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<GuardarItemCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(GuardarItemCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (string.IsNullOrWhiteSpace(command.Descripcion))
            throw new ValidationFailedException("DESCRIPCION_REQUERIDA", "La descripción del ítem es obligatoria.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "guardar_item", planIdTexto,
            new GuardarItemInput(
                command.Id?.ToString(), command.Sku, command.Descripcion.Trim(), command.Pedido, command.Cliente,
                command.Destino, command.SecuenciaEntrega, command.LargoMm, command.AnchoMm, command.AltoMm,
                command.PesoKg, command.Cantidad, command.Color, command.Prioridad, command.TipoEmbalaje,
                command.Apilable, command.MaxNiveles, command.MaxPesoEncimaKg, command.Rotable,
                command.MantenerVertical, command.Fragil, command.Peligroso, command.Refrigerado,
                command.GrupoCompatibilidad, command.Observaciones),
            ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
