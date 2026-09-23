using Nexo.Application.CasosDeUso.Solicitudes.DTOs;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Lectura del piso SICE-TAC y las tarifas históricas (locales, importadas) para el costeo por línea.</summary>
public interface ITarifasCosteoPort
{
    Task<clsPisoSicetacDto?> PisoVigenteAsync(Guid rutaId, int periodoActual, CancellationToken ct);

    Task<IReadOnlyList<clsHistoricaSugeridaDto>> HistoricasSugeridasAsync(
        Guid rutaId, TipologiaCarga? tipologia, DateTimeOffset ahora, int top, CancellationToken ct);
}
