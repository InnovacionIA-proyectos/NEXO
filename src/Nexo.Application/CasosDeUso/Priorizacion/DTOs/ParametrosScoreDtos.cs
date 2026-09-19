namespace Nexo.Application.CasosDeUso.Priorizacion.DTOs;

/// <summary>
/// Los parámetros del score tal como se ven y se envían.
///
/// Es un DTO y no la entidad porque la entidad lleva <c>RowVersion</c>, timestamps de
/// auditoría y el Id de la fila única — nada de eso es asunto de quien recalibra.
/// </summary>
public record ParametrosScoreDto(
    double PesoUrgencia,
    double PesoValor,
    double PesoProbabilidad,
    double PesoTipo,
    double HorizonteHoras,
    decimal ValorReferencia,
    double PuntajeLicitacion,
    double PuntajeSpot,
    double ValorPorDefectoSinValor,
    double ProbabilidadPorDefecto);

/// <summary>
/// Cuántas solicitudes tocó un recálculo.
///
/// <c>Revisadas</c> y <c>Actualizadas</c> van separadas porque la diferencia dice algo:
/// si se revisan 400 y se actualizan 0, el recálculo está corriendo de más y el
/// intervalo se puede espaciar. Con un solo número eso no se ve.
/// </summary>
public record ResultadoDeRecalculoDto(int Revisadas, int Actualizadas, int Lotes);
