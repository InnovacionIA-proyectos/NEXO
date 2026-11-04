namespace Nexo.Domain.Priorizacion.Reglas;

/// <summary>Resultado del cálculo de prioridad: total (0–100) y desglose por componente.</summary>
public sealed record clsResultadoScore(
    double Total,
    double Urgencia,
    double Valor,
    double Probabilidad,
    double Tipo);
