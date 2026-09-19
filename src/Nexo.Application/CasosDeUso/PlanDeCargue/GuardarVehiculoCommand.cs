using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104): da de alta un vehículo/contenedor — POST /api/vehiculos. El
/// vehiculoId lo genera NEXO (criterio 1 de la HU: "genera el vehiculoId"); la fila REAL
/// (dimensiones, ejes, presets) vive únicamente en la base de Cubicador — NEXO no
/// mantiene espejo de vehículos (ver issue #108, limitación documentada: hoy nada en NEXO
/// lee <c>VehiculosCubicacion</c> salvo el stub que <see cref="CrearPlanCommand"/> asegura
/// para no romper su FK). Por eso este comando no toca ninguna fila propia ni resuelve
/// fallos de Cubicador con un aviso — solo publica y responde 202.
/// </summary>
public sealed record GuardarVehiculoCommand(
    string Nombre,
    string? Descripcion,
    decimal LargoInternoMm,
    decimal AnchoInternoMm,
    decimal AltoInternoMm,
    decimal CapacidadKg,
    decimal TaraKg,
    decimal PuertaAnchoMm,
    decimal PuertaAltoMm,
    string PuertaPosicion,
    IReadOnlyList<EjeVehiculoInput> Ejes,
    decimal ToleranciaParedesMm,
    decimal ToleranciaEntreUnidadesMm)
    : ICommand<ComandoAceptadoDto>;

public sealed class GuardarVehiculoCommandHandler(ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<GuardarVehiculoCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(GuardarVehiculoCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ValidationFailedException("NOMBRE_REQUERIDO", "El nombre del vehículo es obligatorio.");

        var vehiculoId = Guid.NewGuid();

        await cubicacion.PublicarComandoAsync(
            "guardar_vehiculo", vehiculoId.ToString(),
            new GuardarVehiculoInput(
                vehiculoId.ToString(), command.Nombre.Trim(), command.Descripcion,
                command.LargoInternoMm, command.AnchoInternoMm, command.AltoInternoMm,
                command.CapacidadKg, command.TaraKg,
                command.PuertaAnchoMm, command.PuertaAltoMm, command.PuertaPosicion,
                command.Ejes, command.ToleranciaParedesMm, command.ToleranciaEntreUnidadesMm),
            ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(vehiculoId, "procesando");
    }
}
