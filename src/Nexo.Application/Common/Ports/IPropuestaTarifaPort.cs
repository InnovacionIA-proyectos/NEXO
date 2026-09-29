using Nexo.Application.Common.Dtos;
using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

/// <summary>
/// Puerto hacia el módulo Biblioteca (motor de propuesta de tarifa: compara
/// plaza SAP, piso SICE-TAC e histórico SAP). Implementado por
/// Infrastructure/Adapters/clsPropuestaTarifaAdapter.cs.
/// </summary>
public interface IPropuestaTarifaPort
{
    Task<clsPropuestaSolicitudDto> ProponerAsync(Guid solicitudId, CancellationToken ct = default);

    /// <summary>
    /// Piso SICE-TAC del viaje VACÍO desde la ciudad de levante del contenedor hasta
    /// el origen del cargue (aprovisionamiento, Res. 16075). Null si el DW no está
    /// configurado o no hay tarifa para la ruta. Lo usa Captura de Solicitudes
    /// (módulo aún no migrado —); el puerto ya queda listo.
    /// </summary>
    Task<clsCandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default);

    /// <summary>
    /// Piso SICE-TAC vigente para una ruta, con el desglose de horas — la misma fuente
    /// (DWSice, período actual) que usa el motor automático de costeo. Lo usa Mesa de
    /// Costeo (issue #179: reemplaza la caché local de import manual, que no tenía
    /// evidencia de uso real y nunca cubría el período vigente). Null si el DW no está
    /// configurado o no hay piso para la ruta/configuración.
    /// </summary>
    Task<clsPisoSicetacVigenteDto?> PisoSicetacVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct = default);
}
