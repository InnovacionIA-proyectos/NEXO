namespace Nexo.Domain.PlanDeCargue.Reglas;

/// <summary>
/// El plan de cargue solo se habilita con los golden tests de paridad del motor en 26/26. No basta un booleano:
/// <paramref name="verificada"/> debe venir con la evidencia concreta (corrida de CI o commit) y la fecha en que
/// se confirmó. Sin los tres la compuerta queda cerrada (fail closed).
/// </summary>
public static class clsCompuertaDeParidad
{
    public static bool EstaHabilitada(bool verificada, string? evidencia, DateOnly? fecha) =>
        verificada && !string.IsNullOrWhiteSpace(evidencia) && fecha is not null;
}
