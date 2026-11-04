using Nexo.Application.CasosDeUso.Cubicacion.DTOs;

namespace Nexo.Application.CasosDeUso.Cubicacion.Interfaces;

/// <summary>
/// Excepción declarada al patrón "NEXO habla con Cubicador solo por colas": <see cref="CrearAsync"/> es síncrono porque la pantalla necesita el resultado al instante.
/// La persistencia es del Cubicador (NEXO no guarda nada); la llave (<c>Cubicador3D:RecomendacionesApiKey</c>) vive solo en el backend.
/// </summary>
public interface ICubicacionRapidaPort
{
    /// <summary>POST /recomendaciones — crea una corrida nueva (append-only).</summary>
    Task<clsRecomendacionDto> CrearAsync(clsCrearRecomendacionInput solicitud, CancellationToken ct = default);

    /// <summary>
    /// GET /recomendaciones?idLineaSolicitud=&amp;origenSistema= — la última corrida de esa línea. <paramref name="origenSistema"/> es obligatorio
    /// y no se infiere de la llave: un mismo código de línea puede existir en NEXO y en Cotifly como rutas distintas.
    /// </summary>
    Task<clsRecomendacionDto> ConsultarAsync(string idLineaSolicitud, string origenSistema, CancellationToken ct = default);
}
