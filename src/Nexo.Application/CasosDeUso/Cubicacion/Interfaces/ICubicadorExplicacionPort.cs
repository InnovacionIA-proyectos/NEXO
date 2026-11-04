namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// Explicación en lenguaje natural de por qué el Cubicador eligió esa flota. Aparte de <see cref="ICubicacionRapidaPort"/> porque el texto lo escribe un modelo
/// (tarda segundos, necesita tiempo de espera propio) y es opcional: <b>nunca lanza</b>; ante cualquier falla devuelve null y la pantalla se dibuja sin el bloque.
/// </summary>
public interface ICubicadorExplicacionPort
{
    /// <summary>La explicación de la última corrida de la línea, o null si no hay.</summary>
    Task<string?> ObtenerAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default);
}
