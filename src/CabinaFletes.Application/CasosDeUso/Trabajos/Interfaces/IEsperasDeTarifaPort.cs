using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>Líneas que esperan a que una persona agregue la tarifa.</summary>
public interface IEsperasDeTarifaPort
{
    /// <summary>
    /// Registra que una línea quedó esperando. Idempotente: el mismo trabajo y la misma
    /// línea no se apuntan dos veces, porque un reintento del mensaje no es una espera
    /// nueva.
    /// </summary>
    Task RegistrarAsync(Guid trabajoId, Guid solicitudId, Guid lineaId, string motivo, CancellationToken ct = default);

    /// <summary>Las que siguen esperando en un trabajo, con su motivo.</summary>
    Task<IReadOnlyList<EsperaDeTarifaDto>> PendientesDeTrabajoAsync(Guid trabajoId, CancellationToken ct = default);
}
