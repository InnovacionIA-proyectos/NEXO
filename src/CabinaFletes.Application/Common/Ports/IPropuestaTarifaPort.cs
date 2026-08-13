using CabinaFletes.Application.Common.Dtos;

namespace CabinaFletes.Application.Common.Ports;

/// <summary>
/// Puerto hacia el módulo Biblioteca (motor de propuesta de tarifa: compara
/// plaza SAP, piso SICE-TAC e histórico SAP). Implementado por
/// Infrastructure/Adapters/PropuestaTarifaAdapter.cs.
/// </summary>
public interface IPropuestaTarifaPort
{
    Task<PropuestaSolicitudDto> ProponerAsync(Guid solicitudId, CancellationToken ct = default);

    /// <summary>
    /// Piso SICE-TAC del viaje VACÍO desde la ciudad de levante del contenedor hasta
    /// el origen del cargue (aprovisionamiento, Res. 16075). Null si el DW no está
    /// configurado o no hay tarifa para la ruta. Lo usa Captura de Solicitudes
    /// (módulo aún no migrado — ver PENDIENTES.md); el puerto ya queda listo.
    /// </summary>
    Task<CandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default);
}
