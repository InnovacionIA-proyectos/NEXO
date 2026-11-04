using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Lectura del piso SICE-TAC y las tarifas históricas (locales, importadas) para el costeo por línea.</summary>
public interface ITarifasCosteoPort
{
    Task<clsPisoSicetacDto?> PisoVigenteAsync(
        string origen, string destino, string? vehiculo, TipologiaCarga? tipologia,
        string? carroceria, CancellationToken ct);

    Task<IReadOnlyList<clsHistoricaSugeridaDto>> HistoricasSugeridasAsync(
        Guid rutaId, TipologiaCarga? tipologia, DateTimeOffset ahora, int top, CancellationToken ct);

    /// <summary>Motivo por el que el piso SICE-TAC NO se usa (el período cargado más reciente no es el del mes actual); null si se usa con normalidad.</summary>
    Task<string?> MotivoSicetacNoUsadoAsync(CancellationToken ct);
}
