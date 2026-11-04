namespace Nexo.Application.CasosDeUso.PlanDeCargue.Interfaces;

/// <summary>Compuerta de liberación por paridad del motor: cerrada, ninguna solicitud de cubicación se publica y se responde "en verificación".</summary>
public interface IParidadCubicadorPort
{
    bool Habilitada { get; }
}
