namespace Nexo.Application.Common.Ports;

/// <summary>
/// Publica un mensaje para que alguien lo procese después. Es transversal: lo usan el
/// auto-costeo, el recálculo de score, el correo y la cubicación.
///
/// Application no sabe que detrás hay una cola, ni cuál. El adaptador de Infrastructure
/// publica a través del Outbox transaccional, lo que da la garantía que importa: el
/// mensaje se guarda en la MISMA transacción que los datos. Si la transacción no
/// confirma, el mensaje no existe; si confirma, el mensaje se entrega aunque el broker
/// esté caído en ese instante.
///
/// Por eso este método no envía nada por sí solo: deja el mensaje listo y quien lo
/// despacha es el commit. Publicar y no llamar a SaveChanges no publica nada.
/// </summary>
public interface IPublicadorDeMensajes
{
    Task PublicarAsync<T>(T mensaje, CancellationToken ct = default) where T : class;
}
