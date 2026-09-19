namespace CabinaFletes.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>Persistencia del resultado del auto-costeo en la línea.</summary>
public interface ILineasCosteadasPort
{
    /// <summary>
    /// Deja la línea como resuelta por el servicio, con su valor y su fuente.
    ///
    /// La fuente se guarda porque la trazabilidad es regla inviolable: toda propuesta
    /// debe poder mostrar de dónde salió. Un valor sin fuente no es auditable.
    /// </summary>
    Task MarcarAutoResueltaAsync(Guid lineaId, decimal valor, string fuente, CancellationToken ct = default);

    /// <summary>
    /// Deja la línea pendiente con el motivo visible: sin tarifa válida (requiere al
    /// Administrador de Tarifas) o una fuente que agotó sus reintentos. Sin esto, el motivo
    /// solo vivía en el mensaje del bus y en el registro de pendientes, sin que ningún DTO
    /// de la línea lo expusiera (issue #131).
    /// </summary>
    Task MarcarPendienteAsync(Guid lineaId, string motivo, CancellationToken ct = default);
}
