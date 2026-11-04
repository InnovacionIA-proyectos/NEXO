using Nexo.Domain.Enums;
using Nexo.Domain.Solicitudes.Entidades;

namespace Nexo.Domain.Reglas;

/// <summary>Transiciones válidas del flujo de cotización, para no dejar una solicitud o cotización en un estado imposible. Transicionar al mismo estado es idempotente.</summary>
public static class clsMaquinaEstados
{
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

    public static bool PuedeEntregar(EstadoCotizacion actual) => actual == EstadoCotizacion.Borrador;

    /// <summary>El resultado se fija una sola vez desde <see cref="ResultadoCotizacion.SinDefinir"/>; repetirlo es idempotente y cambiarlo (Ganada a Perdida) no se permite.</summary>
    public static bool PuedeMarcarResultado(ResultadoCotizacion actual, ResultadoCotizacion destino) =>
        actual == ResultadoCotizacion.SinDefinir || actual == destino;

    /// <summary>Re-marcado idéntico: ya se aplicó y no debe repetir efectos.</summary>
    public static bool EsReMarcadoIdentico(ResultadoCotizacion actual, ResultadoCotizacion destino) =>
        actual != ResultadoCotizacion.SinDefinir && actual == destino;
}
