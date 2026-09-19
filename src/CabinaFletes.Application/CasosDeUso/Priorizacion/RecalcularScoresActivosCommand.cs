using CabinaFletes.Application.CasosDeUso.Priorizacion.DTOs;
using CabinaFletes.Application.CasosDeUso.Priorizacion.Interfaces;
using CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;
using CabinaFletes.Application.Common.Abstractions;
using CabinaFletes.Domain.Priorizacion.Reglas;

namespace CabinaFletes.Application.CasosDeUso.Priorizacion;

/// <summary>
/// Refresca el score PERSISTIDO de las solicitudes activas.
///
/// QUÉ SIRVE Y QUÉ NO. La bandeja recalcula el score al leerlo, porque la urgencia
/// depende del "ahora" y una licitación tiene que subir sola a medida que se acerca su
/// deadline. Así que esto NO ordena la bandeja: mantiene fresco el valor guardado, que
/// es el que leen métricas y tableros. Sin él, los reportes muestran el score del
/// momento de la captura y no se parecen a lo que el usuario ve en pantalla.
///
/// Antes vivía en un BackgroundService dentro del API. Bajó a caso de uso, que es donde
/// debía estar: el ciclo lo lleva la mensajería y esto solo hace el trabajo.
///
/// POR LOTES. La versión anterior traía todas las activas a memoria y guardaba una sola
/// vez. Con volumen real eso es una transacción larga bloqueando filas que la bandeja
/// está leyendo, y un fallo a mitad tira el trabajo completo. Por lotes, lo ya hecho
/// queda hecho y el reintento del mensaje solo repite lo que falta — y repetir es
/// inofensivo, porque el cálculo depende del estado y no de cuántas veces se corrió.
/// </summary>
public sealed record RecalcularScoresActivosCommand(int TamanoDeLote = 200)
    : ICommand<ResultadoDeRecalculoDto>;

public sealed class RecalcularScoresActivosCommandHandler(
    ISolicitudesParaRecalcularPort activas,
    ISolicitudesRepository solicitudes,
    TimeProvider reloj,
    IUnitOfWork uow)
    : ICommandHandler<RecalcularScoresActivosCommand, ResultadoDeRecalculoDto>
{
    public async Task<ResultadoDeRecalculoDto> Handle(
        RecalcularScoresActivosCommand command, CancellationToken ct = default)
    {
        var tamano = Math.Clamp(command.TamanoDeLote, 1, 2_000);

        // Los parámetros se leen UNA vez para toda la corrida. Si alguien recalibra a
        // mitad, este recálculo termina con los de antes y el cambio dispara el suyo;
        // mezclar dos juegos de pesos en la misma pasada dejaría la mitad de las
        // solicitudes en una escala y la otra mitad en otra.
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
                var score = (decimal)ScorePrioridadCalculator.Calcular(s, parametros, ahora).Total;
                if (s.ScorePrioridad == score) continue;

                s.ScorePrioridad = score;
                cambiadasEnElLote++;
            }

            // Solo se guarda si algo cambió. Sin esta guarda, cada pasada escribiría
            // UpdatedAt de todas las activas y ensuciaría la auditoría con cambios que
            // no cambiaron nada.
            if (cambiadasEnElLote > 0)
            {
                await uow.SaveChangesAsync(ct);
                actualizadas += cambiadasEnElLote;
            }

            cursor = lote[^1].Id;
            if (lote.Count < tamano) break;
        }

        return new ResultadoDeRecalculoDto(revisadas, actualizadas, lotes);
    }
}
