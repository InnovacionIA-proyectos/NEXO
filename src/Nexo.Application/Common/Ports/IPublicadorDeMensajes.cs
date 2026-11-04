namespace Nexo.Application.Common.Ports;

/// <summary>
/// Publica un mensaje vía Outbox transaccional: queda guardado en la MISMA transacción que los datos y lo despacha el commit,
/// por eso publicar sin llamar a SaveChanges no publica nada.
/// </summary>
public interface IPublicadorDeMensajes
{
    Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class;
}
