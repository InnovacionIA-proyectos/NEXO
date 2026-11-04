using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Priorizacion.Interfaces;

/// <summary>
/// Solicitudes activas a refrescar, por lotes paginados por identificador (no Skip/Take: el recálculo escribe sobre las
/// mismas filas que ordena y un desplazamiento saltaría solicitudes).
/// </summary>
public interface ISolicitudesParaRecalcularPort
{
    /// <param name="despuesDe">Último identificador procesado. Nulo para el primer lote.</param>
    Task<IReadOnlyList<clsSolicitud>> ActivasAsync(Guid? despuesDe, int tamano, CancellationToken ct = default);
}
