using Nexo.Application.CasosDeUso.Trabajos.DTOs;
using Nexo.Domain.Enums;

namespace Nexo.Application.CasosDeUso.Trabajos.Interfaces;

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
    Task<IReadOnlyList<clsPendienteDeCosteoDto>> DeTrabajoAsync(Guid trabajoId, CancellationToken ct = default);

    /// <summary>
    /// Todas las que esperan intervención humana, de cualquier trabajo. Es lo que se
    /// revisa cuando aparecen tarifas nuevas.
    /// </summary>
    Task<IReadOnlyList<clsLineaPendienteRef>> ManualesAbiertasAsync(CancellationToken ct = default);

    /// <summary>
    /// Cierra el pendiente porque la línea se resolvió. Deja el rastro de cuánto tardó,
    /// que para el tipo Manual es cuánto tardó la intervención de una persona.
    /// </summary>
    Task MarcarResueltoAsync(Guid trabajoId, Guid lineaId, TipoPendienteDeCosteo tipo, CancellationToken ct = default);
}

/// <summary>Referencia mínima a una línea pendiente: lo justo para volver a costearla.</summary>
public sealed record clsLineaPendienteRef(Guid TrabajoId, Guid SolicitudId, Guid LineaId);
