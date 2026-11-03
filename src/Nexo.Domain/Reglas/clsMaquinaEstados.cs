using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Domain.Reglas;

/// <summary>
/// Máquina de estados del flujo de cotización. Concentra en un solo lugar (puro y
/// testeable) qué transiciones son válidas, para que ningún servicio deje una
/// solicitud o cotización en un estado imposible (p. ej. "ganar" una perdida,
/// reabrir una descartada o re-entregar). Transición hacia el mismo estado = idempotente.
/// </summary>
public static class clsMaquinaEstados
{
    // --------------------------------------------------------------- clsSolicitud
    private static readonly Dictionary<EstadoSolicitud, EstadoSolicitud[]> SolicitudDestinos = new()
    {
        [EstadoSolicitud.Pendiente] = [EstadoSolicitud.EnCosteo, EstadoSolicitud.Entregada, EstadoSolicitud.Descartada],
        [EstadoSolicitud.EnCosteo] = [EstadoSolicitud.Entregada, EstadoSolicitud.Descartada],
        [EstadoSolicitud.Entregada] = [],   // terminal
        [EstadoSolicitud.Descartada] = [],  // terminal
    };

    public static bool PuedeTransicionar(EstadoSolicitud actual, EstadoSolicitud destino) =>
        actual == destino || SolicitudDestinos[actual].Contains(destino);

    /// <summary>Estados desde los que la solicitud aún admite edición/costeo.</summary>
    public static bool EsEditable(EstadoSolicitud estado) =>
        estado is EstadoSolicitud.Pendiente or EstadoSolicitud.EnCosteo;

    // --------------------------------------------------------------- Cotización (estado)
    public static bool PuedeEntregar(EstadoCotizacion actual) => actual == EstadoCotizacion.Borrador;

    // --------------------------------------------------------------- Cotización (resultado)
    /// <summary>
    /// El resultado solo puede fijarse una vez desde <see cref="ResultadoCotizacion.SinDefinir"/>.
    /// Marcar el mismo resultado de nuevo es idempotente (no repite efectos); cambiar de
    /// Ganada a Perdida (o viceversa) requiere una corrección explícita, no un re-marcado.
    /// </summary>
    public static bool PuedeMarcarResultado(ResultadoCotizacion actual, ResultadoCotizacion destino) =>
        actual == ResultadoCotizacion.SinDefinir || actual == destino;

    /// <summary>Un re-marcado idéntico: la operación ya se aplicó, no debe repetir efectos.</summary>
    public static bool EsReMarcadoIdentico(ResultadoCotizacion actual, ResultadoCotizacion destino) =>
        actual != ResultadoCotizacion.SinDefinir && actual == destino;
}
