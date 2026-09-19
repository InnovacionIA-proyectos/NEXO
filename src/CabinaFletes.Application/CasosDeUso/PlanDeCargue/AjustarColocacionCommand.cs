using CabinaFletes.Application.CasosDeUso.PlanDeCargue.DTOs;
using CabinaFletes.Application.CasosDeUso.PlanDeCargue.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Common.Ports;
using CabinaFletes.Mensajes.Cubicacion;

namespace CabinaFletes.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// S3-HU-02 (issue #104, criterio 3): ajuste manual de una colocación — PATCH
/// /api/planes-cargue/{id}/colocaciones/{colocacionId}. Deja de ser un PATCH síncrono
/// contra Cubicador: ahora es un comando async, igual que el resto — si Cubicador lo
/// rechaza (posición inválida, colisión, soporte insuficiente...), el aviso de
/// <c>CubicacionRechazadaConsumer</c> por /hubs/plan-cargue es la señal para que el
/// frontend reconcilie su UI optimista.
///
/// Decisión completa (UI optimista + camino de reconciliación) en
/// docs/decisiones/ADR-ajuste-manual-async.md (issue #101, S3-HU-01.1).
/// </summary>
public sealed record AjustarColocacionCommand(
    Guid PlanCargueId,
    Guid ColocacionId,
    decimal? X,
    decimal? Y,
    decimal? Z,
    int? OrientacionIndice,
    bool? Bloqueada,
    bool? Retirar,
    bool? Reinsertar)
    : ICommand<ComandoAceptadoDto>;

public sealed class AjustarColocacionCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<AjustarColocacionCommand, ComandoAceptadoDto>
{
    public async Task<ComandoAceptadoDto> Handle(AjustarColocacionCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new NotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "ajustar_colocacion", planIdTexto,
            new AjustarColocacionInput(
                command.ColocacionId.ToString(), command.X, command.Y, command.Z, command.OrientacionIndice,
                command.Bloqueada, command.Retirar, command.Reinsertar),
            ct);

        await uow.SaveChangesAsync(ct);

        return new ComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
