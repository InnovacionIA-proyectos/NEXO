using Nexo.Application.CasosDeUso.Priorizacion.DTOs;
using Nexo.Application.CasosDeUso.Priorizacion.Interfaces;
using Nexo.Application.CasosDeUso.Solicitudes.Interfaces;
using Nexo.Application.Common.Abstractions;
using Nexo.Domain.Priorizacion.Reglas;

namespace Nexo.Application.CasosDeUso.Priorizacion;

/// <summary>
/// Refresca el score PERSISTIDO (el que leen métricas y tableros); no ordena la bandeja, que recalcula al leer.
/// Va por lotes para no bloquear filas con una transacción larga; reintentar es inofensivo porque el cálculo depende del estado.
/// </summary>
public sealed record clsRecalcularScoresActivosCommand(int TamanoDeLote = 200)
    : ICommand<clsResultadoDeRecalculoDto>;

public sealed class clsRecalcularScoresActivosCommandHandler(
    ISolicitudesParaRecalcularPort activas,
    ISolicitudesRepository solicitudes,
    TimeProvider reloj,
    IUnitOfWork uow)
    : ICommandHandler<clsRecalcularScoresActivosCommand, clsResultadoDeRecalculoDto>
{
    public async Task<clsResultadoDeRecalculoDto> Handle(
        clsRecalcularScoresActivosCommand command, CancellationToken ct = default)
    {
        var tamano = Math.Clamp(command.TamanoDeLote, 1, 2_000);

        // Parámetros leídos UNA vez por corrida: mezclar dos juegos de pesos en la misma pasada dejaría solicitudes en escalas distintas.
        var parametros = await solicitudes.ParametrosScoreAsync(ct);
        var ahora = reloj.GetUtcNow();

        int revisadas = 0, actualizadas = 0, lotes = 0;
        Guid? cursor = null;

        while (true)
        {
            var lote = await activas.ActivasAsync(cursor, tamano, ct);
            if (lote.Count == 0) break;

            lotes++;
            var cambiadasEnElLote = 0;

            foreach (var s in lote)
            {
                revisadas++;
                var score = (decimal)clsScorePrioridadCalculator.Calcular(s, parametros, ahora).Total;
                if (s.ScorePrioridad == score) continue;

                s.ScorePrioridad = score;
                cambiadasEnElLote++;
            }

            // Solo se guarda si algo cambió: evita escribir UpdatedAt de todas las activas en cada pasada.
            if (cambiadasEnElLote > 0)
            {
                await uow.SaveChangesAsync(ct);
                actualizadas += cambiadasEnElLote;
            }

            cursor = lote[^1].Id;
            if (lote.Count < tamano) break;
        }

        return new clsResultadoDeRecalculoDto(revisadas, actualizadas, lotes);
    }
}
