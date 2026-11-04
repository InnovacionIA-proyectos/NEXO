using Nexo.Application.Common.Dtos;
using Nexo.Domain.Enums;

namespace Nexo.Application.Common.Ports;

public interface IPropuestaTarifaPort
{
    Task<clsPropuestaSolicitudDto> ProponerAsync(Guid solicitudId, CancellationToken ct = default);

    /// <summary>
    /// Piso SICE-TAC del viaje VACÍO desde la ciudad de levante del contenedor hasta el origen del cargue (aprovisionamiento, Res. 16075).
    /// Null si el DW no está configurado o no hay tarifa para la ruta.
    /// </summary>
    Task<clsCandidatoTarifaDto?> EstimarAprovisionamientoAsync(
        string ciudadLevante, string origen, string? configuracion, CancellationToken ct = default, string? carroceria = null);

    /// <summary>
    /// Piso SICE-TAC vigente (DWSice, período actual) con el desglose de horas, para Mesa de Costeo.
    /// Null si el DW no está configurado o no hay piso para la ruta/configuración.
    /// </summary>
    Task<clsPisoSicetacVigenteDto?> PisoSicetacVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct = default);

    /// <summary>
    /// El piso SICE-TAC solo se usa si su período es el del mes y año actuales. Devuelve el motivo visible cuando el período más
    /// reciente en DWSice no lo es (la propuesta sale sin SICE-TAC); null si se usa con normalidad o no hay período cargado.
    /// </summary>
    Task<string?> MotivoSicetacNoUsadoAsync(CancellationToken ct = default);
}
