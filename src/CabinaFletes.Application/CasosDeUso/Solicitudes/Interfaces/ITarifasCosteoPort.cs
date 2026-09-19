using CabinaFletes.Application.CasosDeUso.Solicitudes.DTOs;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Solicitudes.Interfaces;

/// <summary>Lectura del piso SICE-TAC y las tarifas históricas (locales, importadas) para el costeo por línea.</summary>
public interface ITarifasCosteoPort
{
    Task<PisoSicetacDto?> PisoVigenteAsync(Guid rutaId, int periodoActual, CancellationToken ct);

    Task<IReadOnlyList<HistoricaSugeridaDto>> HistoricasSugeridasAsync(
        Guid rutaId, TipologiaCarga? tipologia, DateTimeOffset ahora, int top, CancellationToken ct);
}
