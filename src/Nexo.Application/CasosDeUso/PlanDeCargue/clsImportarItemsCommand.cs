using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>Un ítem dentro de un <see cref="clsImportarItemsCommand"/> — mismos campos que
/// <see cref="clsGuardarItemCommand"/> menos el PlanCargueId (lo trae el comando contenedor).</summary>
public sealed record clsItemAImportar(
    string? Sku, string Descripcion, string? Pedido, string? clsCliente, string? Destino, int SecuenciaEntrega,
    decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg, int Cantidad, string? Color, int Prioridad,
    string? TipoEmbalaje, bool Apilable, int MaxNiveles, decimal MaxPesoEncimaKg, bool Rotable,
    bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado, string? GrupoCompatibilidad,
    string? Observaciones);

/// <summary>
/// Importación masiva de ítems (1-2.000) — POST
/// /api/planes-cargue/{id}/items/importar. NEXO valida el rango temprano (400 rápido, sin
/// encolar) — Cubicador vuelve a validarlo del lado del worker, que es quien manda si
/// algo se cuela.
/// </summary>
public sealed record clsImportarItemsCommand(Guid PlanCargueId, IReadOnlyList<clsItemAImportar> Items)
    : ICommand<clsComandoAceptadoDto>;

public sealed class clsImportarItemsCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsImportarItemsCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsImportarItemsCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (command.Items.Count is < 1 or > 2000)
            throw new clsValidationFailedException("IMPORTACION_INVALIDA", "Entre 1 y 2.000 unidades por importación.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "importar_items", planIdTexto,
            new clsImportarItemsInput(command.Items.Select(i => new clsGuardarItemInput(
                null, i.Sku, i.Descripcion, i.Pedido, i.clsCliente, i.Destino, i.SecuenciaEntrega,
                i.LargoMm, i.AnchoMm, i.AltoMm, i.PesoKg, i.Cantidad, i.Color, i.Prioridad, i.TipoEmbalaje,
                i.Apilable, i.MaxNiveles, i.MaxPesoEncimaKg, i.Rotable, i.MantenerVertical, i.Fragil,
                i.Peligroso, i.Refrigerado, i.GrupoCompatibilidad, i.Observaciones)).ToList()),
            ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
