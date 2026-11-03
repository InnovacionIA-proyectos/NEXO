namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// GET /recomendaciones/explicacion del Cubicador: por qué eligió esa flota, en dos o tres frases. Es aparte de
/// <see cref="ICubicacionRapidaPort"/> porque el texto lo escribe un modelo y tarda segundos (necesita un tiempo de espera propio, mayor
/// que el de la recomendación), y porque es opcional: <b>nunca lanza</b>. Sin configurar, con el Cubicador caído, sin responder, con una
/// respuesta que no se entiende o sin recomendación para esa línea, devuelve null y la pantalla se dibuja igual sin el bloque.
/// Por aquí no pasa ninguna tarifa ni valor comercial: solo el texto.
/// </summary>
public interface ICubicadorExplicacionPort
{
    /// <summary>La explicación de la última corrida de la línea, o null si no hay.</summary>
    Task<string?> ObtenerAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default);
}
