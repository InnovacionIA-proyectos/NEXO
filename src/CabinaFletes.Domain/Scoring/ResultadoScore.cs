namespace CabinaFletes.Domain.Scoring;

/// <summary>
/// Resultado del cálculo de prioridad. Además del total (0–100), expone el desglose
/// por componente para alimentar el tooltip del Score Indicator en la Bandeja.
/// </summary>
public sealed record ResultadoScore(
    double Total,
    double Urgencia,
    double Valor,
    double Probabilidad,
    double Tipo);
