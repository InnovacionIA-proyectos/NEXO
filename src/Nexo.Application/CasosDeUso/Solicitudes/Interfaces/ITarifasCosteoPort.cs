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

    /// <summary>#300: motivo por el que el piso SICE-TAC NO se está usando (el período más reciente cargado no es el
    /// del mes actual). Null si se usa con normalidad. Es el aviso que Mesa de Costeo muestra cuando
    /// <see cref="PisoVigenteAsync"/> devuelve null por esa causa.</summary>
    Task<string?> MotivoSicetacNoUsadoAsync(CancellationToken ct);
}
