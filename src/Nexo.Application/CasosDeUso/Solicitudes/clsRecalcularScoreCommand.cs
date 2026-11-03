using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Application.Common.Exceptions;
using Nexo.Domain.Priorizacion.Reglas;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Solicitudes;

/// <summary>Recalcula y persiste el score de prioridad. Devuelve el desglose por componente.</summary>
public sealed record clsRecalcularScoreCommand(Guid SolicitudId) : ICommand<clsDesgloseScoreDto>;

public sealed class clsRecalcularScoreCommandHandler(ISolicitudesRepository solicitudes, TimeProvider clock, IUnitOfWork uow)
    : ICommandHandler<clsRecalcularScoreCommand, clsDesgloseScoreDto>
{
    public async Task<clsDesgloseScoreDto> Handle(clsRecalcularScoreCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new clsNotFoundException("SOLICITUD_NO_ENCONTRADA", "clsSolicitud no encontrada.");

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var r = clsScorePrioridadCalculator.Calcular(s, parametros, clock.GetUtcNow());
        s.ScorePrioridad = (decimal)r.Total;
        await uow.SaveChangesAsync(ct);
        return new clsDesgloseScoreDto(r.Total, r.Urgencia, r.Valor, r.Probabilidad, r.Tipo);
    }
}
