using Nexo.Application.CasosDeUso.Trabajos.DTOs;

namespace Nexo.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>
/// Empuja el avance de un trabajo a quien lo mire (implementado en Api). El aviso NO es garantía: la verdad está en la consulta por identificador,
/// así que un fallo aquí no debe tratarse como fallo del trabajo.
/// </summary>
public interface INotificadorDeTrabajos
{
    Task AvisarAsync(clsAvisoDeTrabajo aviso, CancellationToken ct = default);
}
