using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>Un ítem dentro de un <see cref="ImportarItemsCommand"/> — mismos campos que
/// <see cref="GuardarItemCommand"/> menos el PlanCargueId (lo trae el comando contenedor).</summary>
public sealed record ItemAImportar(
    string? Sku, string Descripcion, string? Pedido, string? Cliente, string? Destino, int SecuenciaEntrega,
    decimal LargoMm, decimal AnchoMm, decimal AltoMm, decimal PesoKg, int Cantidad, string? Color, int Prioridad,
    string? TipoEmbalaje, bool Apilable, int MaxNiveles, decimal MaxPesoEncimaKg, bool Rotable,
    bool MantenerVertical, bool Fragil, bool Peligroso, bool Refrigerado, string? GrupoCompatibilidad,
    string? Observaciones);

/// <summary>
/// S3-HU-02 (issue #104): importación masiva de ítems (1-2.000) — POST
/// /api/planes-cargue/{id}/items/importar. NEXO valida el rango temprano (400 rápido, sin
/// encolar) — Cubicador vuelve a validarlo del lado del worker (issue #105), que es quien
/// manda si algo se cuela.
/// </summary>
public sealed record ImportarItemsCommand(Guid PlanCargueId, IReadOnlyList<ItemAImportar> Items)
    : ICommand<ComandoAceptadoDto>;

public sealed class ImportarItemsCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<ImportarItemsCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(ImportarItemsCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        if (command.Items.Count is < 1 or > 2000)
            throw new ValidationFailedException("IMPORTACION_INVALIDA", "Entre 1 y 2.000 unidades por importación.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "importar_items", planIdTexto,
            new ImportarItemsInput(command.Items.Select(i => new GuardarItemInput(
                null, i.Sku, i.Descripcion, i.Pedido, i.Cliente, i.Destino, i.SecuenciaEntrega,
                i.LargoMm, i.AnchoMm, i.AltoMm, i.PesoKg, i.Cantidad, i.Color, i.Prioridad, i.TipoEmbalaje,
                i.Apilable, i.MaxNiveles, i.MaxPesoEncimaKg, i.Rotable, i.MantenerVertical, i.Fragil,
                i.Peligroso, i.Refrigerado, i.GrupoCompatibilidad, i.Observaciones)).ToList()),
            ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
