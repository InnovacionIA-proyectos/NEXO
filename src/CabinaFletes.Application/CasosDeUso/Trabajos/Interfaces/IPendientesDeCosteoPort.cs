using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;
using CabinaFletes.Domain.Enums;

namespace CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>Líneas que quedaron sin costear, con el porqué.</summary>
public interface IPendientesDeCosteoPort
{
    /// <summary>
    /// Registra que una línea quedó pendiente. Idempotente por trabajo, línea y tipo:
    /// un reintento del mensaje no es un pendiente nuevo, y sin esto la misma línea
    /// aparecería repetida tantas veces como se reentregara.
    /// </summary>
    Task RegistrarAsync(Guid trabajoId, Guid solicitudId, Guid lineaId,
                        TipoPendienteDeCosteo tipo, string motivo, CancellationToken ct = default);

    /// <summary>Las que siguen pendientes en un trabajo, con su tipo y motivo.</summary>
    Task<IReadOnlyList<PendienteDeCosteoDto>> DeTrabajoAsync(Guid trabajoId, CancellationToken ct = default);
}
