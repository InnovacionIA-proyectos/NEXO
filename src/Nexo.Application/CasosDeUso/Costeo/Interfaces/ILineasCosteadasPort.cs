namespace Nexo.Application.CasosDeUso.Costeo.Interfaces;

/// <summary>Persistencia del resultado del auto-costeo en la línea.</summary>
public interface ILineasCosteadasPort
{
    /// <summary>Deja la línea resuelta por el servicio con su valor y su fuente (sin fuente un valor no es auditable).</summary>
    Task MarcarAutoResueltaAsync(Guid lineaId, decimal valor, string fuente, CancellationToken ct = default);

    /// <summary>Deja la línea pendiente con el motivo visible (sin tarifa válida, o fuente que agotó sus reintentos).</summary>
    Task MarcarPendienteAsync(Guid lineaId, string motivo, CancellationToken ct = default);

    /// <summary>La línea fue RECHAZADA a propósito por el Administrador de Tarifas: el costeo asíncrono no debe tocarla ni pisarle la decisión.</summary>
    Task<bool> EstaRechazadaAsync(Guid lineaId, CancellationToken ct = default);
}
