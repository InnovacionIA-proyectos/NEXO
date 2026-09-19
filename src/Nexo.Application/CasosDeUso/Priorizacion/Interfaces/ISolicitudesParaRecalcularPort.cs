using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Application.CasosDeUso.Priorizacion.Interfaces;

/// <summary>
/// Las solicitudes activas cuyo score hay que refrescar, POR LOTES.
///
/// Por lotes y no todas de golpe. El servicio anterior traía cada solicitud activa a
/// memoria y las guardaba en una sola transacción: con volumen real eso es una
/// transacción larga bloqueando filas que el usuario está leyendo en la bandeja, y un
/// fallo a mitad tira el trabajo entero.
///
/// El lote se ordena por identificador y se pagina por él en vez de por Skip/Take: el
/// recálculo escribe sobre las mismas filas que ordena, así que con desplazamiento se
/// saltarían solicitudes sin que nadie se enterara.
/// </summary>
public interface ISolicitudesParaRecalcularPort
{
    /// <param name="despuesDe">Último identificador procesado. Nulo para el primer lote.</param>
    /// <param name="tamano">Cuántas traer como máximo.</param>
    Task<IReadOnlyList<clsSolicitud>> ActivasAsync(Guid? despuesDe, int tamano, CancellationToken ct = default);
}
