using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Application.Common.Exceptions;
using CabinaFletes.Application.Solicitudes.Dtos;
using CabinaFletes.Application.Solicitudes.Ports;
using CabinaFletes.Domain.Scoring;

namespace CabinaFletes.Application.Solicitudes.Commands;

/// <summary>Recalcula y persiste el score de prioridad. Devuelve el desglose por componente.</summary>
public sealed record RecalcularScoreCommand(Guid SolicitudId) : ICommand<DesgloseScoreDto>;

public sealed class RecalcularScoreCommandHandler(ISolicitudesRepository solicitudes, TimeProvider clock, IUnitOfWork uow)
    : ICommandHandler<RecalcularScoreCommand, DesgloseScoreDto>
{
    public async Task<DesgloseScoreDto> Handle(RecalcularScoreCommand command, CancellationToken ct = default)
    {
        var s = await solicitudes.ObtenerBasicoAsync(command.SolicitudId, ct)
            ?? throw new NotFoundException("SOLICITUD_NO_ENCONTRADA", "Solicitud no encontrada.");

        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var r = ScorePrioridadCalculator.Calcular(s, parametros, clock.GetUtcNow());
        s.ScorePrioridad = (decimal)r.Total;
        await uow.SaveChangesAsync(ct);
        return new DesgloseScoreDto(r.Total, r.Urgencia, r.Valor, r.Probabilidad, r.Tipo);
    }
}
