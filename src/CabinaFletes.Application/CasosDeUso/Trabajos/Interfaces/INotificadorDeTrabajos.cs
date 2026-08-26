using CabinaFletes.Application.CasosDeUso.Trabajos.DTOs;

namespace CabinaFletes.Application.CasosDeUso.Trabajos.Interfaces;

/// <summary>
/// Empuja el avance de un trabajo a quien lo esté mirando.
///
/// Es un puerto porque el canal es un detalle: hoy SignalR, mañana lo que sea. Quien
/// lo implementa vive en Api, que es donde está ASP.NET Core; Application solo sabe que
/// existe una forma de avisar.
///
/// EL AVISO NO ES UNA GARANTÍA. Si falla, nadie se entera y no pasa nada grave: la
/// verdad está en la consulta por identificador. Por eso quien llame a esto NO debe
/// tratar un fallo aquí como un fallo del trabajo — sería tumbar un pliego costeado
/// porque un navegador se desconectó.
/// </summary>
public interface INotificadorDeTrabajos
{
    Task AvisarAsync(AvisoDeTrabajo aviso, CancellationToken ct = default);
}
