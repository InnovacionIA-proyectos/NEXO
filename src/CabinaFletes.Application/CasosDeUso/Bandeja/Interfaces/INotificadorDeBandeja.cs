using CabinaFletes.Application.CasosDeUso.Bandeja.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Bandeja.Interfaces;

/// <summary>
/// Empuja a quien tenga la bandeja abierta que vuelva a consultarla.
///
/// Es un puerto porque SignalR es de Api y ni Application ni Infrastructure pueden verlo.
///
/// UN FALLO AL AVISAR NO ES UN FALLO DE NADA. Si el aviso no sale, la bandeja sigue
/// siendo correcta la próxima vez que alguien la consulte; lo único que se pierde es la
/// actualización automática. Quien implemente esto no debe dejar subir la excepción:
/// mandaría a la cola de error un mensaje por cada navegador que se desconectó.
/// </summary>
public interface INotificadorDeBandeja
{
    Task AvisarAsync(AvisoDeBandeja aviso, CancellationToken ct = default);
}
