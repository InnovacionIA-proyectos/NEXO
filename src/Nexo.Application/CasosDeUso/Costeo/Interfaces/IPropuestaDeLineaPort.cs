using Nexo.Domain.Costeo.Reglas;

namespace Nexo.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>
/// Propuesta de UNA línea (consulta las fuentes y aplica la regla del mayor); por línea para que el fallo de una fuente
/// deje pendiente solo esa línea. Devuelve <see cref="clsDecisionDeFlujo"/>: su <c>Modo</c> decide si se auto-guarda el
/// flete o la línea espera a una persona (ConsultaCompleta), aunque <c>Resultado.Valor</c> no sea null.
/// </summary>
public interface IPropuestaDeLineaPort
{
    /// <remarks>Se llama ProponerParaLinea porque C# no permite sobrecargar por tipo de retorno.</remarks>
    Task<clsDecisionDeFlujo> ProponerParaLineaAsync(Guid lineaId, CancellationToken ct = default);
}
