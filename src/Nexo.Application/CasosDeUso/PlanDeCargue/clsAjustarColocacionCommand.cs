using Nexo.Application.CasosDeUso.PlanDeCargue.DTOs;
using Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Application.Common.Ports;
using Nexo.Mensajes.Cubicacion;

namespace Nexo.Application.CasosDeUso.PlanDeCargue;

/// <summary>
/// Ajuste manual de una colocación — PATCH
/// /api/planes-cargue/{id}/colocaciones/{colocacionId}. Deja de ser un PATCH síncrono
/// contra Cubicador: ahora es un comando async, igual que el resto — si Cubicador lo
/// rechaza (posición inválida, colisión, soporte insuficiente...), el aviso de
/// <c>clsCubicacionRechazadaConsumer</c> por /hubs/plan-cargue es la señal para que el
/// frontend reconcilie su UI optimista.
///
/// Decisión completa (UI optimista + camino de reconciliación) en
/// docs/decisiones/ADR-ajuste-manual-async.md.
/// </summary>
public sealed record clsAjustarColocacionCommand(
    Guid PlanCargueId,
    Guid ColocacionId,
    decimal? X,
    decimal? Y,
    decimal? Z,
    int? OrientacionIndice,
    bool? Bloqueada,
    bool? Retirar,
    bool? Reinsertar)
    : ICommand<clsComandoAceptadoDto>;

public sealed class clsAjustarColocacionCommandHandler(
    IPlanCargueRepository planes, ICubicacionPort cubicacion, IUnitOfWork uow)
    : ICommandHandler<clsAjustarColocacionCommand, clsComandoAceptadoDto>
{
    public async Task<clsComandoAceptadoDto> Handle(clsAjustarColocacionCommand command, CancellationToken ct = default)
    {
        _ = await planes.ObtenerAsync(command.PlanCargueId, ct)
            ?? throw new clsNotFoundException("PLAN_CARGUE_NO_ENCONTRADO", "Plan de cargue no encontrado.");

        var planIdTexto = command.PlanCargueId.ToString();

        await cubicacion.PublicarComandoAsync(
            "ajustar_colocacion", planIdTexto,
            new clsAjustarColocacionInput(
                command.ColocacionId.ToString(), command.X, command.Y, command.Z, command.OrientacionIndice,
                command.Bloqueada, command.Retirar, command.Reinsertar),
            ct);

        await uow.SaveChangesAsync(ct);

        return new clsComandoAceptadoDto(command.PlanCargueId, "procesando");
    }
}
