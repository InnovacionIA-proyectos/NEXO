namespace Nexo.Application.CasosDeUso.Priorizacion.DTOs;

/// <summary>Los parámetros del score sin RowVersion ni datos de auditoría de la entidad.</summary>
public record clsParametrosScoreDto(
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

/// <summary>Revisadas y Actualizadas van separadas: muchas revisadas con 0 actualizadas indica que el recálculo corre de más.</summary>
public record clsResultadoDeRecalculoDto(int Revisadas, int Actualizadas, int Lotes);
