using CabinaFletes.Domain.Costeo.Reglas;

namespace CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>
/// Propuesta de UNA línea: consulta las fuentes y aplica la regla del mayor.
///
/// Por línea y no por pliego a propósito. El puerto que existía costeaba la solicitud
/// completa, y con eso una excepción consultando una fuente se llevaba las 300 líneas.
/// La regla inviolable 6 dice lo contrario: el fallo de una fuente deja ESA línea
/// pendiente y el resto del pliego sigue. Por línea, el fallo queda contenido.
///
/// Devuelve el resultado de dominio, no un DTO: quien decide qué hacer con él es el
/// caso de uso, y la decisión ya está tomada por la regla.
/// </summary>
public interface IPropuestaDeLineaPort
{
    /// <remarks>
    /// Se llama ProponerParaLinea y no Proponer porque el adaptador implementa también
    /// el puerto por solicitud, y las dos firmas serían idénticas salvo el tipo de
    /// retorno — algo que C# no permite sobrecargar.
    /// </remarks>
    Task<ResultadoDeCosteo> ProponerParaLineaAsync(Guid lineaId, CancellationToken ct = default);
}
