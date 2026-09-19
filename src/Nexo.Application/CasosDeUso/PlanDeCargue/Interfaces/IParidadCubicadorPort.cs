namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>
/// S2-HU-08.2 (#30): compuerta de liberación por paridad del motor. Cuando está
/// cerrada, ninguna solicitud de cubicación llega a publicarse a la cola — se
/// responde "en verificación" antes de tocar nada.
/// </summary>
public interface IParidadCubicadorPort
{
    bool Habilitada { get; }
}
