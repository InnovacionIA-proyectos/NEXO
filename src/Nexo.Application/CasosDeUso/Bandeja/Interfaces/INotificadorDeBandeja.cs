using Nexo.Application.CasosDeUso.Bandeja.DTOs;

namespace Nexo.Application.CasosDeUso.Bandeja.Interfaces;

/// <summary>
/// Empuja a quien tenga la bandeja abierta que la vuelva a consultar (puerto porque SignalR es de Api).
/// Un fallo al avisar no es un fallo de nada: quien implemente esto no debe dejar subir la excepción, mandaría a la cola de error un mensaje por cada navegador desconectado.
/// </summary>
public interface INotificadorDeBandeja
{
    Task AvisarAsync(clsAvisoDeBandeja aviso, CancellationToken ct = default);
}
